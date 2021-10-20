﻿using BillingAPI.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BillingAPI.Filters
{
    public class EvarunAuthorizationFilter : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {

        }
        private void AddCharacter(ActionExecutingContext context, string value)
        {
            int character;
            if (int.TryParse(value, out character))
            {
                object model;
                if (context.ActionArguments.ContainsKey("character"))
                {
                    context.ActionArguments["character"] = character;
                }
                else if(context.ActionArguments.TryGetValue("request", out model) && model is CharacterBasedRequest)
                {
                    int.TryParse(value, out int characterId);
                    ((CharacterBasedRequest)model).Character = characterId;
                }
                else
                {
                    context.ActionArguments.Add("character", character);
                }

            }
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if(context.HttpContext.Request.Headers.ContainsKey("X-User-Id"))
            {
                AddCharacter(context, context.HttpContext.Request.Headers["X-User-Id"]);
            }
            else
            {
                //TODO disable it on prod
                //AddCharacter(context, "9570");
#if (DEBUG)

#else
                //context.Result = new RedirectResult(@"https://rc-web.evarun.ru/login?externalUrl=");
#endif
                //TODO redirect
            }

        }


    }
}
