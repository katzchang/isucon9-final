using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace cs
{
    //HttpResponseExceptionをスローした場合に、キャッチしてプロパティで指定したステータスレスポンスを返すフィルター
    //https://tech.tanaka733.net/entry/2020/02/use-exceptions-to-modify-the-response
    public class HttpResponseExceptionFilter : IActionFilter, IOrderedFilter
    {
        public int Order { get; set; } = int.MaxValue - 10;

        public void OnActionExecuting(ActionExecutingContext context) { }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is HttpResponseException exception)
            {
                Console.WriteLine(exception);
                context.Result = new ObjectResult(new 
                {
                    is_error = true,
                    message = exception.Message
                })
                {
                    StatusCode = exception.Status,
                };
                context.ExceptionHandled = true;
            }
            else if (context.Exception != null)
            {
                Console.WriteLine("Unhandled exception!!!");
                Console.WriteLine(context.Exception);
            }
        }
    }
}