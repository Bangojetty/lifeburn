using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Server.Effects;

namespace Server.Controllers;

public class LifeExceptionFilter : Attribute, IExceptionFilter {
    public void OnException(ExceptionContext context) {
        if (context.Exception is InvalidDataException) {
            context.Result = new ObjectResult("invalid data") {
                StatusCode = (int)HttpStatusCode.BadRequest
            };
            context.ExceptionHandled = true;
        } else if (context.Exception is UnauthorizedAccessException) {
            context.Result = new UnauthorizedResult();
            context.ExceptionHandled = true;
        }
    }
}