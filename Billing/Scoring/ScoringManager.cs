﻿using Billing;
using Billing.Dto;
using Billing.Dto.Scoring;
using Core;
using Core.Exceptions;
using Core.Model;
using Core.Primitives;
using Dapper;
using IoC;
using Microsoft.EntityFrameworkCore;
using Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Scoringspace
{
    public interface IScoringManager : IBaseBillingRepository
    {
        Task OnPillConsumed(int model, string pillLifestyle);
        Task OnWounded(int model);
        Task OnClinicalDeath(int model);
        Task OnDumpshock(int model);
        Task OnFoodConsume(int model, string foodLifeStyle);
        Task OnOtherBuy(SIN sin, int lifestyle);
        Task OnMatrixBuy(SIN sin, int lifestyle);
        Task OnDroneBuy(SIN sin, int lifestyle);
        Task OnPillBuy(SIN sin, int lifestyle);
        Task OnWeaponBuy(SIN sin, int lifestyle);
        Task OnMagicBuy(SIN sin, int lifestyle);
        Task OnInsuranceBuy(SIN sin, int lifestyle);
        Task OnInsuranceChanged(SIN sin, bool hasInsurance);
        Task OnCharityBuy(SIN sin, int lifestyle);
        Task OnFoodBuy(SIN sin, int lifestyle);
        Task OnImplantBuy(SIN sin, int lifestyle);
        Task OnImplantInstalled(int model, string implantlifestyle, string autodoclifestyle);
        Task OnImplantUninstalled(int model, string autodoclifestyle);
        Task OnImplantDeletedBlack(int model);
        Task OnImplantDeletedActive(int model);
        Task OnMetatypeChanged(SIN sin);
        ScoringDto GetFullScoring(int character);
        ScoringCategoryDto AddScoringCategory(string categoryName, bool relative, decimal weight);
        void DeleteScoringCategory(int id);
        ScoringCategoryDto UpdateCategoryWeight(int id, decimal weight);
        ScoringFactorDto UpdateFactorCategory(int factorId, int categoryId);
        List<ScoringCategoryDto> GetScoringCategories(bool? relative);
        List<ScoringFactorDto> GetScoringFactors(int categoryId);
        List<ScoringEventLifeStyleDto> GetFactorEvents(int factorId);
        ScoringEventLifeStyleDto AddScoringEvent(int factorId, int lifestyle, decimal value);
        void DeleleteScoringEvent(int factorId, int lifestyle);
    }

    public class ScoringManager : BaseBillingRepository, IScoringManager
    {
        #region implementation

        public async Task OnInsuranceBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.insurance);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnInsuranceChanged(SIN sin, bool hasInsurance)
        {
            var factorId = GetFactorId(ScoringFactorEnum.insurance_change);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var eventn = hasInsurance ? 1 : 0;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == eventn);
                return value?.Value ?? 1;
            });
        }

        public async Task OnMatrixBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_matrix);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }
        public async Task OnDroneBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_drone);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }
        public async Task OnCharityBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_charity);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnMetatypeChanged(SIN sin)
        {
            var factorId = GetFactorId(ScoringFactorEnum.metatype);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == sin.Passport.MetatypeId);
                return value?.Value ?? 1;
            });
        }

        public async Task OnImplantInstalled(int model, string implantlifestyle, string autodoclifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.implant_install);
            var scoring = GetScoringByModelId(model);
            if (!BillingHelper.LifestyleIsDefined(implantlifestyle) || !BillingHelper.LifestyleIsDefined(autodoclifestyle))
            {
                return;
            }
            var lifestyle = BillingHelper.GetLifestyle(implantlifestyle);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var ls = (int)lifestyle;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == ls);
                return value?.Value ?? 1;
            });
            factorId = GetFactorId(ScoringFactorEnum.where_implant_install);
            lifestyle = BillingHelper.GetLifestyle(autodoclifestyle);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var ls = (int)lifestyle;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == ls);
                return value?.Value ?? 1;
            });

        }

        public async Task OnImplantUninstalled(int model, string autodoclifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.implant_uninstalled);
            var scoring = GetScoringByModelId(model);
            var lifestyle = BillingHelper.GetLifestyle(autodoclifestyle);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var ls = (int)lifestyle;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == ls);
                return value?.Value ?? 1;
            });
        }

        public async Task OnImplantDeletedBlack(int model)
        {
            var factorId = GetFactorId(ScoringFactorEnum.implant_deleted);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == 2);
                return value?.Value ?? 1;
            });
        }

        public async Task OnImplantDeletedActive(int model)
        {
            var factorId = GetFactorId(ScoringFactorEnum.implant_deleted);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == 1);
                return value?.Value ?? 1;
            });
        }

        public async Task OnImplantBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_implant);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnOtherBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_other);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnPillBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_pill);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnWeaponBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_weapon);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnMagicBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_magic);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnFoodBuy(SIN sin, int lifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.buy_food);
            await RaiseScoringEvent(sin.ScoringId ?? 0, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == lifestyle);
                return value?.Value ?? 1;
            });
        }

        public async Task OnFoodConsume(int model, string foodLifeStyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.food_consume);
            if (!BillingHelper.LifestyleIsDefined(foodLifeStyle))
            {
                return;
            }
            var lifestyle = BillingHelper.GetLifestyle(foodLifeStyle);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var ls = (int)lifestyle;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == ls);
                return value?.Value ?? 1;
            });
        }

        public async Task OnPillConsumed(int model, string pillLifestyle)
        {
            var factorId = GetFactorId(ScoringFactorEnum.pill_use);
            if (!BillingHelper.LifestyleIsDefined(pillLifestyle))
            {
                return;
            }
            var lifestyle = BillingHelper.GetLifestyle(pillLifestyle);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var ls = (int)lifestyle;
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == ls);
                return value?.Value ?? 1;
            });
        }

        public async Task OnWounded(int model)
        {
            var factorId = GetFactorId(ScoringFactorEnum.worse);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == 1);
                return value?.Value ?? 1;
            });
        }

        public async Task OnClinicalDeath(int model)
        {
            var factorId = GetFactorId(ScoringFactorEnum.clinical_death);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == 1);
                return value?.Value ?? 1;
            });
        }

        public async Task OnDumpshock(int model)
        {
            var factorId = GetFactorId(ScoringFactorEnum.dumpshock);
            var scoring = GetScoringByModelId(model);
            await RaiseScoringEvent(scoring.Id, factorId, (context) =>
            {
                var value = context.Set<ScoringEventLifestyle>().AsNoTracking().FirstOrDefault(s => s.ScoringFactorId == factorId && s.EventNumber == 1);
                return value?.Value ?? 1;
            });
        }
        #endregion

        private List<CurrentScoringCategoryDto> GetScoringResultView(List<CurrentFactor> factors, decimal currentresult)
        {
            return factors.GroupBy(f => f.CurrentCategory)
                .Select(g => new CurrentScoringCategoryDto(g.Key, factors.Select(f => f.CurrentCategory).Distinct().Sum(cc => cc.Value), currentresult)
                {
                    Factors = g.Select(f => new ScoringFactorDto(f.ScoringFactor)
                    {
                        Value = Math.Round(f.Value, 2),
                    }).ToList()
                }).ToList();
        }

        public ScoringDto GetFullScoring(int character)
        {
            var scoring = GetScoringByModelId(character);
            var fixenum = (int)ScoringCategoryType.Fix;
            var relativenum = (int)ScoringCategoryType.Relative;
            var currentFix = Math.Round(scoring.CurrentFix, 2);
            var currentRelative = Math.Round(scoring.CurerentRelative, 2);


            var fixFactors = GetList<CurrentFactor>(f => f.CurrentCategory.Category.CategoryType == fixenum && f.CurrentCategory.ScoringId == scoring.Id, f => f.ScoringFactor, f => f.CurrentCategory.Category);
            var relativFactors = GetList<CurrentFactor>(f => f.CurrentCategory.Category.CategoryType == relativenum && f.CurrentCategory.ScoringId == scoring.Id, f => f.ScoringFactor, f => f.CurrentCategory.Category);
            var fixCategories = GetScoringResultView(fixFactors, currentFix);
            var relativCategories = GetScoringResultView(relativFactors, currentRelative);

            return new ScoringDto
            {
                Character = character,
                CurrentFix = currentFix,
                CurrentRelative = currentRelative,
                FixCategories = fixCategories,
                RelativeCategories = relativCategories
            };
        }

        public ScoringCategoryDto AddScoringCategory(string categoryName, bool relative, decimal weight)
        {
            var sc = new ScoringCategory
            {
                CategoryType = relative ? (int)ScoringCategoryType.Relative : (int)ScoringCategoryType.Fix,
                Name = categoryName,
                Weight = weight
            };
            AddAndSave(sc);
            return new ScoringCategoryDto(sc);
        }

        public ScoringCategoryDto UpdateCategoryWeight(int id, decimal weight)
        {
            var sc = Get<ScoringCategory>(c => c.Id == id);
            if (sc == null)
                throw new BillingNotFoundException($"категория {id} не найдена");
            sc.Weight = weight;
            SaveContext();
            return new ScoringCategoryDto(sc);
        }

        public ScoringFactorDto UpdateFactorCategory(int factorId, int categoryId)
        {
            var sf = Get<ScoringFactor>(c => c.Id == factorId);
            if (sf == null)
                throw new BillingNotFoundException($"фактор {sf} не найдена");
            sf.CategoryId = categoryId;
            SaveContext();
            return new ScoringFactorDto(sf);
        }

        public List<ScoringCategoryDto> GetScoringCategories(bool? relative)
        {
            var all = true;
            int type = 0;
            if (relative.HasValue)
            {
                all = false;
                type = relative.Value ? (int)ScoringCategoryType.Relative : (int)ScoringCategoryType.Fix;
            }
            var list = GetList<ScoringCategory>(sc => all || sc.CategoryType == type);
            return list.Select(s => new ScoringCategoryDto(s)).ToList();
        }

        public List<ScoringFactorDto> GetScoringFactors(int categoryId)
        {
            var list = GetList<ScoringFactor>(sf => sf.CategoryId == categoryId || categoryId == -1, f => f.Category).Select(s => new ScoringFactorDto(s)).ToList();
            return list;
        }

        public List<ScoringEventLifeStyleDto> GetFactorEvents(int factorId)
        {
            var list = GetList<ScoringEventLifestyle>(se => se.ScoringFactorId == factorId).Select(se => new ScoringEventLifeStyleDto(se)).ToList();
            return list;
        }

        public ScoringEventLifeStyleDto AddScoringEvent(int factorId, int lifestyle, decimal value)
        {
            var eventls = Get<ScoringEventLifestyle>(se => se.ScoringFactorId == factorId && se.EventNumber == lifestyle);
            if (eventls != null)
                throw new BillingException("eventlifestyle уже заведен");
            var newevent = new ScoringEventLifestyle
            {
                EventNumber = lifestyle,
                ScoringFactorId = factorId,
                Value = value
            };
            AddAndSave(newevent);
            return new ScoringEventLifeStyleDto(newevent);
        }

        public void DeleleteScoringEvent(int factorId, int lifestyle)
        {
            var eventls = Get<ScoringEventLifestyle>(se => se.ScoringFactorId == factorId && se.EventNumber == lifestyle);
            if (eventls == null)
                throw new BillingException("eventlifestyle не найден");
            RemoveAndSave(eventls);
        }

        public void DeleteScoringCategory(int id)
        {
            var sc = Get<ScoringCategory>(c => c.Id == id);
            if (sc == null)
                throw new BillingNotFoundException($"категория {id} не найдена");
            RemoveAndSave(sc);
        }

        #region mathematic

        private Task RaiseScoringEvent(int scoringId, int factorId, Func<BillingContext, decimal> action)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var context = new BillingContext())
                    {
                        using (var dbContextTransaction = context.Database.BeginTransaction())
                        {
                            var connection = context.Database.GetDbConnection();
                            var id = connection.QueryFirstOrDefault<int>($"SELECT id FROM scoring  WHERE id = {scoringId} FOR UPDATE;");//block scoring for updates
                            var start = DateTime.Now;
                            var lifestyle = action(context);
                            var factor = context.Set<ScoringFactor>().AsNoTracking().FirstOrDefault(f => f.Id == factorId);
                            var category = context.Set<ScoringCategory>().AsNoTracking().FirstOrDefault(f => f.Id == factor.CategoryId);
                            var scoring = context.Set<Scoring>().AsTracking().FirstOrDefault(s => s.Id == scoringId);
                            var systemsettings = IocContainer.Get<ISettingsManager>();
                            var oldScoring = scoring.CurerentRelative + scoring.CurrentFix;
                            var curCategory = context.Set<CurrentCategory>().AsNoTracking().Include(f => f.Category)
                                                        .FirstOrDefault(c => c.ScoringId == scoringId && c.CategoryId == factor.CategoryId);
                            if (curCategory == null)
                            {
                                curCategory = new CurrentCategory
                                {
                                    ScoringId = scoringId,
                                    CategoryId = factor.CategoryId,
                                    Category = category,
                                    Value = 1
                                };
                                Add(curCategory, context);
                            }
                            var curFactor = context.Set<CurrentFactor>().AsNoTracking()
                                                        .FirstOrDefault(s => s.CurrentCategoryId == curCategory.Id && s.ScoringFactorId == factorId);
                            if (curFactor == null)
                            {
                                curFactor = new CurrentFactor
                                {
                                    ScoringFactorId = factorId,
                                    CurrentCategoryId = curCategory.Id,
                                    Value = scoring.StartFactor ?? 1
                                };
                                Add(curFactor, context);
                            }
                            var oldFactorValue = curFactor.Value;

                            var newValue = CalculateFactor((double)lifestyle, (double)curFactor.Value);
                            curFactor.Value = newValue;

                            Add(curFactor, context);
                            var curFactors = context.Set<CurrentFactor>().AsNoTracking().Include(f => f.ScoringFactor)
                                                        .Where(f => f.CurrentCategoryId == curCategory.Id).ToList();

                            var allCates = context.Set<CurrentCategory>().AsNoTracking()
                                                    .Where(c => c.ScoringId == scoringId && c.Category.CategoryType == category.CategoryType && c.CurrentFactors.Count > 0).ToList();


                            var factorsCount = curFactors.Count;
                            if (factorsCount == 0)
                            {
                                factorsCount = 1;
                            }
                            var oldCatValue = curCategory.Value;


                            var curCatCount = allCates.Count;
                            var k = (decimal)Math.Pow((curCatCount > 0 ? curCatCount : 2) * 2, -1);
                            var averFactors = curFactors.Sum(f => f.Value) / factorsCount;
                            var catWeight = curCategory?.Category?.Weight;
                            curCategory.Value = (decimal)Math.Pow((double)averFactors, (double)GetCatWeight(catWeight ?? 0));
                            Add(curCategory, context);
                            var newCatValue = curCategory.Value;
                            if (category.CategoryType == (int)ScoringCategoryType.Fix)
                            {
                                scoring.CurrentFix = allCates.Sum(c => c.Value) * k;
                            }
                            else if (category.CategoryType == (int)ScoringCategoryType.Relative)
                            {
                                scoring.CurerentRelative = allCates.Sum(c => c.Value) * k;
                            }
                            Add(scoring, context);
                            var end = DateTime.Now;
                            var scoringEvent = new ScoringEvent
                            {
                                FinishTime = end,
                                StartTime = start,
                                CurrentFactor = curFactor,
                                OldFactorValue = oldFactorValue,
                                NewFactorValue = newValue,
                                OldCategoryValue = oldCatValue,
                                NewCategoryValue = newCatValue,
                                OldScoring = oldScoring,
                                NewScoring = scoring.CurerentRelative + scoring.CurrentFix,
                                SaveK = k,
                                AverFactors = averFactors
                            };
                            Add(scoringEvent, context);
                            dbContextTransaction.Commit();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
            });
        }

        private decimal GetCatWeight(decimal weight)
        {
            return (weight > 1 || weight < 0 ? 0 : weight);
        }

        private decimal CalculateFactor(double lifestyle, double current)
        {
            var result = (1 + Math.Sqrt(Math.Abs(lifestyle))) * (lifestyle + Math.Abs(lifestyle)) * (current + Math.Sqrt(1 / (10 * current))) / (4 * lifestyle)
                + Math.Sqrt(1 / Math.Abs(lifestyle)) * ((lifestyle - Math.Abs(lifestyle)) * (Math.Sqrt(current + 0.25) - 0.5) / (2 * lifestyle));
            if (result > 3)
                result = 3;
            if (result < 0.3)
                result = 0.3;
            return (decimal)result;
        }

        #endregion

        #region private
        private int GetFactorId(ScoringFactorEnum factorName)
        {
            using (var context = new BillingContext())
            {
                var factor = context.Set<ScoringFactor>().AsNoTracking().FirstOrDefault(f => f.Code == factorName.ToString());
                return factor?.Id ?? 0;
            }
        }
        private void Add<T>(T entity, BillingContext context) where T : BaseEntity
        {
            if (entity.Id > 0)
                context.Entry(entity).State = EntityState.Modified;
            else
                context.Entry(entity).State = EntityState.Added;
            context.SaveChanges();
        }

        private Scoring GetScoringByModelId(string model)
        {
            int modelId;
            if (!int.TryParse(model, out modelId))
                throw new Exception("model must be int");
            return GetScoringByModelId(modelId);
        }

        private Scoring GetScoringByModelId(int modelId)
        {
            var sin = GetSINByModelId(modelId, s => s.Scoring);
            if (sin.Scoring == null)
                throw new Exception("scoring not found");
            return sin.Scoring;
        }



        #endregion
    }
}
