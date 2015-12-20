using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Prosyn.ApplicationServices.IntegrationTests.Email
{
    public class GuerrillaMail : IDisposable
    {
        /* 
         * TmpMail
         * -------------------------------------------------------------
         * Quick and easy Temp email class for GuerrillaMail
         * Get new email, get messages, send messages then dispose of it
         * 
         * Free to use for whatever purpose
         * https://github.com/Ezzpify/
         *
        */

        /// <summary>
        /// Class variables
        /// </summary>
        private string _sHost = "http://api.guerrillamail.com/ajax.php?";

        private string _wProxy;
        private CookieContainer _cCookies;
        private JavaScriptSerializer _jss;

        /// <summary>
        /// Email variables
        /// </summary>
        private string _sTimeStamp;

        private string _sAlias;
        private string _sSidToken;

        /// <summary>
        /// Initializer for the class.
        /// </summary>
        /// <param name="proxy">Include a Proxy adress string if the request should go via a proxy</param>
        public GuerrillaMail(string proxy = "")
        {
            /*If we got passed a Proxy variable*/
            if (!string.IsNullOrEmpty(proxy))
            {
                /*Regex to match a proxy*/
                const string validIpRegex = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]):[\d]+$";
                if (Regex.IsMatch(proxy, validIpRegex))
                {
                    /*Proxy was in a valid format*/
                    _wProxy = proxy;
                }
            }

            /*Initialize stuff*/
            _cCookies = new CookieContainer();
            _jss = new JavaScriptSerializer();

            /*Initialize the email*/
            var dict = _jss.Deserialize<Dictionary<string, string>>(Contact("f=get_email_address"));
            _sTimeStamp = dict["email_timestamp"];
            _sAlias = dict["email_addr"].Split('@')[0];
            _sSidToken = dict["sid_token"];

            /*Delete the automatic welcome email - id is always 1*/
            DeleteSingleEmail("1");
        }


        /// <summary>
        /// Returns all emails in a json string
        /// offset=0 implies getting all emails
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object[]> GetAllEmails()
        {
            var dict = _jss.Deserialize<dynamic>(Contact("f=get_email_list&offset=0"));
            return dict["list"];
        }


        /// <summary>
        /// Returns all emails received after a specific email (specified by mail_id)
        /// Example: GetEmailsSinceID(53451833)
        /// </summary>
        /// <param name="mailId">mail_id of an email</param>
        /// <returns></returns>
        public Dictionary<string, object[]> GetEmailsSinceId(string mailId)
        {
            var dict = _jss.Deserialize<dynamic>(Contact("f=check_email&seq=" + mailId));
            return dict["list"];
        }


        /// <summary>
        /// Returns the last email
        /// If there are no emails it will return empty Dictionary
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetLastEmail()
        {
            /*Get all emails and select index 0*/
            var emails = _jss.Deserialize<dynamic>(Contact("f=get_email_list&offset=0"));

            if (((object[]) emails["list"]).Any())
            {
                var lastEmail = emails["list"][0];

                /*Null check*/
                if (lastEmail != null)
                {
                    /*Return last email if it is not null*/
                    return lastEmail;
                }   
            }
            
            /*last email is null, return empty dictionary*/
            return new Dictionary<string, object>();
        }


        /// <summary>
        /// Returns our email with a specified domain
        /// </summary>
        /// <param name="domain">Specifies which domain to return (0-8) useful for services that blocks certain domains</param>
        /// <returns></returns>
        public string GetMyEmail(int domain = 0)
        {
            /*Email adress string*/
            var email = string.Format("{0}@", _sAlias);

            /*There are several email domains you can use by default*/
            switch (domain)
            {
                case 1:
                    return email + "grr.la";
                case 2:
                    return email + "guerrillamail.biz";
                case 3:
                    return email + "guerrillamail.com";
                case 4:
                    return email + "guerrillamail.de";
                case 5:
                    return email + "guerrillamail.net";
                case 6:
                    return email + "guerrillamail.org";
                case 7:
                    return email + "guerrillamailblock.com";
                case 8:
                    return email + "spam4.me";
                default:
                    return email + "sharklasers.com";
            }
        }


        /// <summary>
        /// Deletes an array of emails from the mailbox
        /// </summary>
        /// <param name="mailIds">String array of mail_ids</param>
        public void DeleteEmails(string[] mailIds)
        {
            /*If there are at least 1 ID in the array*/
            if (mailIds.Length > 0)
            {
                /*Go through each array value and format delete string*/
                var idString = string.Empty;
                foreach (var id in mailIds)
                {
                    /*Example: &email_ids[]53666&email_ids[]53667*/
                    idString += string.Format("&email_ids[]{0}", id);
                }

                /*Delete the emails*/
                Contact("f=del_email" + idString);
            }
        }


        /// <summary>
        /// Deletes a single email
        /// </summary>
        /// <param name="mailId">mail_id of an email</param>
        public void DeleteSingleEmail(string mailId)
        {
            Contact("f=del_email&email_ids[]=" + mailId);
        }


        /// <summary>
        /// Contacts the specified host(url) to get information
        /// Calling this method refreshes all mail received etc
        /// </summary>
        /// <param name="url"></param>
        private string Contact(string parameters)
        {

            /*Set up the request*/
            var request = (HttpWebRequest) WebRequest.Create(_sHost + parameters);
            request.CookieContainer = _cCookies;
            request.Method = "GET";
            request.Host = "www.guerrillamail.com";
            request.UserAgent =
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            request.Proxy = new WebProxy(_wProxy);

            /*Fetch the response*/
            using (var response = (HttpWebResponse) request.GetResponse())
            {
                /*Check if the response status is okay*/
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    /*Get the stream*/
                    using (var stream = response.GetResponseStream())
                    {
                        /*Get the full string*/
                        var reader = new StreamReader(stream, Encoding.UTF8);
                        return reader.ReadToEnd();
                    }
                }
            }

            /*Something messed up, returning empty string*/
            return string.Empty;
        }


        /// <summary>
        /// Dispose object
        /// </summary>
        public void Dispose()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
