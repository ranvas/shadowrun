﻿using Billing.Dto;
using Billing.Dto.Shop;
using Billing.DTO;
using Core;
using Core.Exceptions;
using Core.Model;
using Core.Primitives;
using InternalServices;
using InternalServices.EreminModel;
using IoC;
using Scoringspace;
using Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Billing
{
    public interface IBillingManager : IAdminManager
    {
        #region application

        Transfer CreateTransferMIRSIN(int characterTo, decimal amount);

        string GetSinStringByCharacter(int modelId);
        int GetModelIdBySinString(string sinString);
        TransferSum GetTransfers(int modelId);
        BalanceDto GetBalance(int modelId);
        RentaSumDto GetRentas(int modelId);
        #endregion

        #region web
        PriceShopDto GetPriceByQR(int character, string qrid);
        PriceShopDto GetPrice(int modelId, int shop, int sku);
        void BreakContract(int corporation, int shop);
        Contract CreateContract(int corporation, int shop);
        RentaDto ConfirmRenta(int modelId, int priceId, int beat, int count = 1);
        List<SkuDto> GetSkuDtos(int corporationId, int nomenklaturaId, bool? enabled, int id = -1);
        ProductType GetExtProductType(string name);
        Nomenklatura GetExtNomenklatura(string name);
        Sku GetExtSku(string name);
        List<Contract> GetContrats(int shopid, int corporationId);
        #endregion

        #region jobs

        BeatCharacterLocalDto ProcessCharacterBeat(int sinId, WorkModelDto workDto);

        #endregion

        #region admin
        List<SIN> GetSinsInGame();
        List<CharacterDto> GetCharactersInGame();
        void FillSku(int corporationId);
        #endregion

        #region events
        void DropInsurance(int modelId);
        SIN DropCharacter(int modelId);
        SIN RestoreCharacter(int modelId);
        #endregion

    }

    public class BillingManager : AdminManager, IBillingManager
    {
        public void BreakContract(int corporation, int shop)
        {
            var contract = Get<Contract>(c => c.CorporationId == corporation && c.ShopId == shop);
            if (contract == null)
                throw new BillingException("Контракт не найден");
            Remove(contract);
            SaveContext();
        }

        public Contract CreateContract(int corporation, int shop)
        {
            var contract = GetAsNoTracking<Contract>(c => c.CorporationId == corporation && c.ShopId == shop);
            if (contract != null)
                throw new BillingException("Контракт уже заключен");
            //TODO CONTRACT LIMIT
            var newContract = new Contract
            {
                ShopId = shop,
                CorporationId = corporation,
                Status = (int)ContractStatusEnum.Approved
            };
            Add(newContract);
            SaveContext();
            return newContract;
        }

        public List<Contract> GetContrats(int shopid, int corporationId)
        {
            var list = GetListAsNoTracking<Contract>(c => (c.ShopId == shopid || shopid == 0) && (c.CorporationId == corporationId || corporationId == 0));
            return list;
        }

        public ProductType GetExtProductType(string name)
        {
            return GetAsNoTracking<ProductType>(p => p.Name == name);
        }

        public Nomenklatura GetExtNomenklatura(string name)
        {
            return GetAsNoTracking<Nomenklatura>(p => p.Name == name);
        }

        public Sku GetExtSku(string name)
        {
            return GetAsNoTracking<Sku>(p => p.Name == name);
        }

        public List<SkuDto> GetSkuDtos(int corporationId, int nomenklaturaId, bool? enabled, int id = -1)
        {
            var list = GetSkus(s => (s.CorporationId == corporationId || corporationId == 0)
                && (s.NomenklaturaId == nomenklaturaId || nomenklaturaId == 0)
                && (s.Enabled == enabled || !enabled.HasValue)
                && (s.Id == id || id == -1));
            return list;
        }

        public RentaSumDto GetRentas(int modelId)
        {
            var result = new TransferSum();
            var sin = GetSINByModelId(modelId, s => s.Wallet, s => s.Character);
            if (sin == null)
                throw new BillingException("sin not found");
            var sum = new RentaSumDto();
            var list = GetListAsNoTracking<Renta>(r => r.Sin.Character.Model == modelId,
                r => r.Sku.Nomenklatura.Specialisation.ProductType,
                r => r.Sku.Corporation,
                r => r.Shop,
                r => r.Sin.Passport,
                r => r.Sin.Character)
                .Select(r =>
                    new RentaDto(r))
                    .ToList();
            sum.Rentas = list;
            sum.Sum = list.Sum(r => r.FinalPrice);
            return sum;

        }

        public RentaDto ConfirmRenta(int modelId, int priceId, int beat, int count = 1)
        {
            var renta = CreateRenta(modelId, priceId, beat, count);
            EreminPushAdapter.SendNotification(modelId, "Покупка совершена", $"Вы купили {renta.Sku.Name}");
            var dto = new RentaDto(renta);
            return dto;
        }

        private decimal GetDividends(CorporationWallet mortagee, decimal percent, decimal defaultValue)
        {
            decimal sum = 0;
            if (mortagee != null)
            {
                sum = mortagee.LastSkuSold * percent;
            }
            if (sum < defaultValue)
                sum = defaultValue;
            return sum;
        }

        public BeatCharacterLocalDto ProcessCharacterBeat(int sinId, WorkModelDto workDto)
        {
            var sw = new Stopwatch();
            sw.Start();
            var localDto = new BeatCharacterLocalDto();
            var sin = BlockCharacter(sinId, s => s.Wallet, s => s.Character, s => s.Passport, s => s.Scoring);
            SaveContext();
            var mir = GetMIR();
            decimal income = 0;
            decimal outcome = 0;
            var mortagee = GetMortagee(sin.Passport);
            //ability
            if (workDto.Dividends1)
            {
                var def1 = _settings.GetDecimalValue(SystemSettingsEnum.dividents1_k);
                var sum = GetDividends(mortagee, 0.03m, def1);
                AddNewTransfer(mir, sin.Wallet, sum, "Дивиденды *");
                income += sum;
                localDto.SumDividends += sum;
            }
            if (workDto.Dividends2)
            {
                var def2 = _settings.GetDecimalValue(SystemSettingsEnum.dividents2_k);
                var sum = GetDividends(mortagee, 0.05m, def2);
                AddNewTransfer(mir, sin.Wallet, sum, "Дивиденды **");
                income += sum;
                localDto.SumDividends += sum;
            }
            if (workDto.Dividends3)
            {
                var def3 = _settings.GetDecimalValue(SystemSettingsEnum.dividents3_k);
                var sum = GetDividends(mortagee, 0.08m, def3);
                AddNewTransfer(mir, sin.Wallet, sum, "Дивиденды ***");
                income += sum;
                localDto.SumDividends += sum;
            }
            //karma
            if (workDto.KarmaCount > 0)
            {
                var k = _settings.GetDecimalValue(SystemSettingsEnum.karma_k);
                var karmasum = k * workDto.KarmaCount;
                income += karmasum;
                localDto.SumKarma += karmasum;
                AddNewTransfer(mir, sin.Wallet, karmasum, "Пассивный доход");
            }
            var swIncome = Cut(sw);
            //rentas
            var rentas = GetList<Renta>(r => r.SinId == sin.Id, r => r.Shop.Wallet, r => r.Sku.Corporation.Wallet);
            foreach (var renta in rentas)
            {
                ProcessRenta(renta, mir, sin);
            }
            var swRentas = Cut(sw);
            //overdrafts
            if (sin.Wallet.Balance > 0)
            {
                var allOverdrafts = GetList<Transfer>(t => t.Overdraft && t.WalletFromId == sin.Wallet.Id && t.RentaId > 0);
                foreach (var overdraft in allOverdrafts)
                {
                    overdraft.Overdraft = false;
                    var closingRenta = Get<Renta>(r => r.Id == overdraft.RentaId, r => r.Sku.Corporation, r => r.Shop.Wallet);
                    CloseOverdraft(closingRenta, mir, sin);
                }
            }
            var swoverdrafts = Cut(sw);
            //metatype
            if (sin.Passport.MetatypeId != sin.OldMetaTypeId)
            {
                var scoring = IoC.IocContainer.Get<IScoringManager>();
                scoring.OnMetatypeChanged(sin);
                sin.OldMetaTypeId = sin.Passport.MetatypeId;
            }
            //insurance
            var insurance = GetInsurance(sin.CharacterId);
            if (insurance?.LifeStyle != sin.OldInsurance)
            {
                if ((insurance?.LifeStyle ?? 0) > 0 != (sin.OldInsurance ?? 0) > 0)
                {
                    var scoring = IoC.IocContainer.Get<IScoringManager>();
                    scoring.OnInsuranceChanged(sin, (insurance?.LifeStyle ?? 0) > 0);
                }
                sin.OldInsurance = insurance?.LifeStyle;
            }
            //summary
            localDto.Scoringvalue = BillingHelper.GetFullScoring(sin.Scoring);
            //forecast
            outcome += rentas.Sum(r => BillingHelper.GetFinalPrice(r));
            if (workDto.StockGainPercentage > 0)
            {
                var stock = outcome * workDto.StockGainPercentage;
                income += stock;
                AddNewTransfer(mir, sin.Wallet, stock, "Игра на бирже");
            }
            localDto.SumRents += outcome;
            sin.Wallet.IncomeOutcome = income - outcome;
            localDto = AddLifeStyle(sin.Wallet, localDto);
            SaveContext();
            var swOther = Cut(sw);
            sw.Stop();
            sin.DebugTime = Serialization.Serializer.ToJSON(new { swIncome, swRentas, swoverdrafts, swOther });
            UnblockCharacter(sin);
            return localDto;
        }

        private double Cut(Stopwatch sw)
        {
            var result = sw.Elapsed.TotalSeconds;
            sw.Restart();
            return result;
        }

        private BeatCharacterLocalDto AddLifeStyle(Wallet wallet, BeatCharacterLocalDto dto)
        {
            if (wallet.IsIrridium)
            {
                dto.IsIrridium = true;
                return dto;
            }
            dto.Balance = wallet.Balance;
            dto.Forecast = BillingHelper.GetForecast(wallet);
            return dto;
        }

        public PriceShopDto GetPriceByQR(int modelId, string qrid)
        {
            var qr = long.Parse(qrid);
            QRHelper.Parse(qr, out int skuId, out int shopId);
            if (skuId == 0 || shopId == 0)
            {
                throw new BillingException($"Ошибка распознования qr");
            }
            return GetPrice(modelId, shopId, skuId);
        }

        public PriceShopDto GetPrice(int modelId, int shopid, int skuid)
        {
            var sin = BillingBlocked(modelId, s => s.Scoring, s => s.Passport, s => s.Character);
            var sku = SkuAllowed(shopid, skuid, s => s.Corporation, s => s.Nomenklatura.Specialisation.ProductType);
            var shop = GetAsNoTracking<ShopWallet>(s => s.Id == shopid);
            if (shop == null || sin == null)
                throw new Exception("some went wrong");
            var price = CreateNewPrice(sku, shop, sin);
            var dto = new PriceShopDto(new PriceDto(price, true), sku.Corporation);
            return dto;
        }

        public BalanceDto GetBalance(int modelId)
        {
            var sin = GetSINByModelId(modelId, s => s.Wallet, s => s.Scoring, s => s.Passport.Metatype);
            var insur = GetInsurance(modelId, r => r.Sku);
            var lics = ProductTypeEnum.Licences.ToString();
            var licences = GetList<Renta>(r => r.Sku.Nomenklatura.Specialisation.ProductType.Alias == lics && r.SinId == sin.Id, r => r.Sku.Nomenklatura)
                .OrderByDescending(r => r.DateCreated)
                .GroupBy(l => l.Sku.NomenklaturaId)
                .Select(g => g.FirstOrDefault()?.Sku?.Name)
                .ToList();
            var lifestyle = BillingHelper.GetLifeStyleDto();
            var balance = new BalanceDto(sin)
            {
                ModelId = modelId,
                LifeStyle = lifestyle.GetLifeStyle(sin.Wallet).ToString(),
                ForecastLifeStyle = lifestyle.GetForecastLifeStyle(sin.Wallet).ToString(),
                Insurance = insur?.Sku?.Name ?? "нет страховки",
                Licenses = licences
            };
            return balance;
        }

        public TransferSum GetTransfers(int modelId)
        {
            var result = new TransferSum();
            var sin = GetSINByModelId(modelId, s => s.Wallet, s => s.Passport);
            if (sin == null)
                throw new BillingException("sin not found");
            var listFrom = GetListAsNoTracking<Transfer>(t => t.WalletFromId == sin.WalletId, t => t.WalletFrom, t => t.WalletTo);
            var listFromTruncated = listFrom.Where(t => t.RentaId == null).ToList();
            var listTo = GetListAsNoTracking<Transfer>(t => t.WalletToId == sin.WalletId, t => t.WalletFrom, t => t.WalletTo);
            var owner = BillingHelper.GetPassportName(sin.Passport);
            result.Transfers = CreateTransfersDto(listFromTruncated, listTo, owner);
            result.Transfers.Insert(0, CreateFakeTransferDto(-1, listFrom.Where(r => r.RentaId != null && !r.Overdraft).Sum(t => t.Amount), false, "Выплаченная сумма по рентам", owner, false));
            result.Transfers.Insert(0, CreateFakeTransferDto(-2, listFrom.Where(r => r.RentaId != null && r.Overdraft).Sum(t => t.Amount), false, "Задолженность по рентам", owner, false));
            return result;
        }

        private TransferDto CreateFakeTransferDto(int id, decimal amount, bool anon, string comment, string owner, bool overdraft)
        {
            return new TransferDto
            {
                Amount = amount,
                Anonimous = anon,
                Comment = comment,
                From = owner,
                OperationTime = DateTime.Now.ToUniversalTime(),
                Overdraft = overdraft,
                To = "MIR",
                TransferType = TransferType.Outcoming.ToString(),
                Id = id
            };
        }

        public string GetSinStringByCharacter(int modelId)
        {
            var sin = GetSINByModelId(modelId, s => s.Passport);
            if (sin == null)
                throw new Exception("sin not found");
            return sin.Passport.Sin;
        }

        public int GetModelIdBySinString(string sinString)
        {
            var sin = GetSINBySinText(sinString, s => s.Character);
            if (sin == null)
                throw new Exception("sin not found");
            return sin.Character.Model;
        }

        public ProductType CreateOrUpdateProductType(int id, string name, int discounttype = 1, int externalId = 0)
        {
            ProductType type = null;
            if (id > 0)
                type = Get<ProductType>(p => p.Id == id);
            if (type == null)
            {
                type = new ProductType();
                Add(type);
            }
            if (discounttype != 0)
                type.DiscountType = discounttype;
            if (!Enum.IsDefined(typeof(DiscountType), type.DiscountType))
            {
                type.DiscountType = (int)DiscountType.Gesheftmaher;
            }
            if (externalId != 0)
            {
                type.ExternalId = externalId;
            }
            if (!string.IsNullOrEmpty(name))
                type.Name = name;
            SaveContext();
            return type;
        }

        public Transfer CreateTransferMIRSIN(int characterTo, decimal amount)
        {
            var to = BillingBlocked(characterTo, s => s.Wallet);
            var from = GetMIR();
            var comment = "Перевод от международного банка";
            var transfer = AddNewTransfer(from, to.Wallet, amount, comment);
            SaveContext();
            return transfer;
        }

        public FullUserDto GetFullUser(int modelid)
        {
            return new FullUserDto(modelid);
        }
        public List<SIN> GetSinsInGame()
        {
            return GetSinsInGame(s => s.Character, s => s.Wallet, s => s.Scoring, s => s.Passport);
        }

        public List<CharacterDto> GetCharactersInGame()
        {
            var result = GetSinsInGame(s => s.Character, s => s.Passport).Select(s => new CharacterDto { PersonName = s.Passport?.PersonName, ModelId = s.Character.Model.ToString() }).ToList();
            return result;
        }
        public void FillSku(int corporationId)
        {
            var corporation = Get<CorporationWallet>(c => c.Id == corporationId);
            if (corporation == null)
                throw new BillingNotFoundException("Corporation not found");
            var specialissations = GetList<CorporationSpecialisation>(c => c.CorporationId == corporationId);
            foreach (var specialisation in specialissations)
            {
                var nomenklaturas = GetList<Nomenklatura>(n => n.SpecialisationId == specialisation.SpecialisationId, n => n.Specialisation);
                foreach (var nomenklatura in nomenklaturas)
                {
                    var sku = Get<Sku>(s => s.NomenklaturaId == nomenklatura.Id && s.CorporationId == corporationId);
                    if (sku != null)
                    {
                        continue;
                    }
                    var enabled = true;
                    if (nomenklatura.Specialisation.Name.Contains("анлок"))
                        enabled = false;
                    sku = new Sku
                    {
                        CorporationId = corporationId,
                        Count = nomenklatura.BaseCount,
                        Enabled = enabled,
                        Name = $"{nomenklatura.Name} ({corporation.Name})",
                        NomenklaturaId = nomenklatura.Id,
                        Price = nomenklatura.BasePrice * specialisation.Ratio
                    };
                    AddAndSave(sku);
                }
            }
        }

        public void DropInsurance(int modelId)
        {
            var insurance = GetInsurance(modelId);
            if (insurance != null)
            {
                insurance.Expired = true;
                SaveContext();
            }
        }

        public SIN DropCharacter(int modelId)
        {
            var sin = GetSINByModelId(modelId, s => s.Wallet);
            sin.InGame = false;
            var rents = GetList<Renta>(r => r.SinId == sin.Id);
            SaveContext();
            foreach (var renta in rents)
            {
                if (!string.IsNullOrEmpty(renta.QRRecorded))
                {
                    try
                    {
                        CleanRenta(renta);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                }
            }
            return sin;
        }
        public SIN RestoreCharacter(int modelId)
        {
            var sin = GetDisabledByModelId(modelId);
            sin.InGame = true;
            SaveContext();
            return sin;
        }

        #region private

        private SIN BlockCharacter(int sinId, params Expression<Func<SIN, object>>[] includes)
        {
            var sin = Get(s => s.Id == sinId, includes);
            sin.Blocked = true;
            return sin;
        }

        private void UnblockCharacter(SIN sin)
        {
            sin.Blocked = false;
            SaveContext();
        }

        private List<SIN> GetSinsInGame(params Expression<Func<SIN, object>>[] includes)
        {
            return GetList(s => (s.InGame ?? false) && s.Character.Game == CURRENTGAME, includes);
        }

        protected Renta GetInsurance(int modelId, params Expression<Func<Renta, object>>[] includes)
        {
            var inss = ProductTypeEnum.Insurance.ToString();
            var insurance = GetList(r => r.Sku.Nomenklatura.Specialisation.ProductType.Alias == inss && r.Sin.Character.Model == modelId && !r.Expired, includes)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            return insurance;
        }
        #endregion
    }
}
