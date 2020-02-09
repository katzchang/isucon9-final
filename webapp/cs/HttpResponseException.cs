
using System;

namespace cs
{
    public class HttpResponseException : Exception
    {
        public int Status { get; set; }

        public HttpResponseException(int status, string message, Exception innerException)
         : base(message, innerException)
        {
            Status = status;
        }

        public HttpResponseException(int status, Exception innerException)
         : base(innerException.Message, innerException)
        {
            Status = status;
        }

        public HttpResponseException(int status, string message)
         : base(message)
        {
            Status = status;
        }
    }
}