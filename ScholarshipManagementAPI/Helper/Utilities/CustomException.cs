namespace ScholarshipManagementAPI.Helper.Utilities
{
    public class CustomException : Exception
    {
        public int StatusCode { get; }

        public CustomException(string message) : base(message)
        {
            StatusCode = 400;
        }

        public CustomException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
