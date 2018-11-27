using System;
using System.Collections.Generic;
using System.Linq;

namespace GzipStreamExtensions.GZipTest.Facilities
{
    public class ResponseContainer
    {
        public IList<string> Messages { get; private set; }
        public bool Success { get; protected set; }

        public ResponseContainer(bool? success = null)
        {
            Success = success ?? false;
            Messages = new List<string>();
        }

        public void AddMessage(string message)
        {
            Messages.Add(message);
        }

        public void AddErrorMessage(string message)
        {
            Success = false;
            AddMessage(message);
        }

        public string MergeMessages(string separator = null)
        {
            var defaultSeparator = Environment.NewLine;

            if (!string.IsNullOrEmpty(separator) && !string.Equals(separator, defaultSeparator, StringComparison.InvariantCulture))
                defaultSeparator = separator;

            var result = string.Join(defaultSeparator, Messages.ToArray());
            return result;
        }

        public void Join(ResponseContainer responseContainer)
        {
            if (responseContainer == null)
                throw new ArgumentNullException(nameof(responseContainer));

            foreach(var message in responseContainer.Messages)
            {
                Messages.Add(message);
            }

            Messages = Messages.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            Success &= responseContainer.Success;
        }
    }

    public sealed class ResponseContainer<T> : ResponseContainer
    {
        public ResponseContainer(bool? success = null) : base(success)
        {
        }

        public T Value { get; private set; }

        public void SetSuccessValue(T value)
        {
            Success = true;
            Value = value;
        }
    }
}
