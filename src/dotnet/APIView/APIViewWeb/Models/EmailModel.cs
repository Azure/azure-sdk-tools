// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class EmailModel
    {
        public string EmailTo { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

        public EmailModel(string emailTo, string subject, string body)
        {
            EmailTo = emailTo;
            Subject = subject;
            Body = body;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
