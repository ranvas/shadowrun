﻿using Billing;
using Billing.Dto;
using Billing.DTO;
using Billing.Excel;
using CommonExcel;
using CommonExcel.Model;
using Core.Model;
using Core.Primitives;
using InternalServices;
using IoC;
using Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Billing
{
    public class ExcelManager
    {

        public List<ExcelError> UploadBillingInit(Stream file, string filename)
        {
            List<ExcelError> errors;
            var reader = new ExcelReader();
            var excel = reader.ParseRows<BillingInitDto>(filename, file, out errors);
            if (excel == null)
                throw new Exception("model not found");
            var manager = IocContainer.Get<IBillingManager>();
            foreach (var item in excel)
            {
                try
                {
                    var db = new BillingInit
                    {
                        Citizenship = item.Citizenship,
                        Model = int.Parse(item.Model),
                        Nation = item.Nation,
                        StartCash = decimal.Parse(item.StartCash),
                        StartFak = decimal.Parse(item.StartFak),
                        Status = item.Status
                    };
                    manager.AddAndSave(db);
                }
                catch (Exception e)
                {

                    Console.WriteLine(e.ToString());
                }

            }
            return errors;
        }

        public MemoryStream LoadTransfers()
        {
            var billing = IocContainer.Get<IBillingManager>();
            var sins = billing.GetSinsInGame();
            var allTransfers = new List<TransferDto>();
            foreach (var sin in sins)
            {
                var transfers = billing.GetTransfers(sin.Character.Model);
                var filteredTransfers = transfers.Transfers.Where(t => t.TransferType == TransferType.Incoming.ToString());
                allTransfers.AddRange(filteredTransfers);
            }
            return ExcelWriter.CreateExcelAsStream(allTransfers);
        }

        public MemoryStream LoadRents()
        {
            var billing = IocContainer.Get<IBillingManager>();
            var sins = billing.GetSinsInGame();
            var result = new List<RentaDto>();
            foreach (var sin in sins)
            {
                var rentas = billing.GetRentas(sin.Character.Model);
                result.AddRange(rentas.Rentas);
            }
            return ExcelWriter.CreateExcelAsStream(result);
        }

        public MemoryStream LoadMainExcel()
        {
            var billing = IocContainer.Get<IBillingManager>();
            var settings = IocContainer.Get<ISettingsManager>();
            var sins = billing.GetSinsInGame();
            var result = new List<MainExcelDto>();
            var ikarkoef = settings.GetDecimalValue(SystemSettingsEnum.ikar_k);
            var karmakoef = settings.GetDecimalValue(SystemSettingsEnum.karma_k);
            var inflationkoef = settings.GetDecimalValue(SystemSettingsEnum.pre_inflation);
            var lifestyle = BillingHelper.GetLifeStyleDto();
            foreach (var sin in sins)
            {
                var allRents = billing.GetRentas(sin.Character.Model);
                decimal karma = 0;
                try
                {
                    var eService = new EreminService();
                    karma = eService.GetCharacter(sin.Character.Model)?.workModel?.karma?.spent ?? 0;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                var excelCharacter = new MainExcelDto
                {
                    Balance = sin.Wallet.Balance,
                    PersonName = sin.Passport?.PersonName,
                    KarmaKoef = karmakoef,
                    InflationKoef = inflationkoef,
                    ScoringFix = sin.Scoring.CurrentFix,
                    ScoringRelative = sin.Scoring.CurerentRelative,
                    ModelId = sin.Character.Model.ToString(),
                    LifeStyle = lifestyle.GetLifeStyle(sin.Wallet).ToString(),
                    SumRents = allRents.Sum,
                    Karma = karma
                };
                result.Add(excelCharacter);
            }

            return ExcelWriter.CreateExcelAsStream(result);
        }
    }
}
