﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace Genso.Astrology.Library
{
    /// <summary>
    /// A collection of general functions that don't have a home yet, so they live here for now.
    /// You're allowed to move them somewhere you see fit, not copy, move!
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// Converts xml element instance to string properly
        /// </summary>
        public static string XmlToString(XElement xml)
        {
            //remove all formatting, for clean xml as string
            return xml.ToString(SaveOptions.DisableFormatting);
        }


        /// <summary>
        /// Converts any type to XML, it will use Type's own ToXml() converter if available
        /// else ToString is called and placed inside element with Type's full name
        /// Note, used to transfer data via internet Client to API Server
        /// Example:
        /// <TypeName>
        ///     DataValue
        /// </TypeName>
        /// </summary>
        public static XElement AnyTypeToXml<T>(T value)
        {
            //check if type has own ToXml method
            //use the Type's own converter if available
            if (value is IToXml hasToXml)
            {
                var betterXml = hasToXml.ToXml();
                return betterXml;
            }

            //gets enum value as string to place inside XML
            //note: value can be null hence ?, fails quietly
            var enumValueStr = value?.ToString();

            //get the name of the Enum
            //Note: This is the name that will be used
            //later to instantiate the class from string
            var typeName = typeof(T).FullName;

            return new XElement(typeName, enumValueStr);
        }

        /// <summary>
        /// Converts any type that implements IToXml to XML, it will use Type's own ToXml() converter
        /// Note, used to transfer data via internet Client to API Server
        /// Placed inside "Root" xml
        /// Default name for root element is Root
        /// </summary>
        public static XElement AnyTypeToXmlList<T>(List<T> xmlList, string rootElementName = "Root") where T : IToXml
        {
            var rootXml = new XElement(rootElementName);
            foreach (var xmlItem in xmlList)
            {
                rootXml.Add(AnyTypeToXml(xmlItem));
            }
            return rootXml;
        }


        /// <summary>
        /// - Type is a value typ
        /// - Enum
        /// </summary>
        public static dynamic XmlToAnyType<T>(XElement xml) // where T : //IToXml, new()
        {
            //get the name of the Enum
            var typeNameFullName = typeof(T).FullName;
            var typeNameShortName = typeof(T).FullName;

#if DEBUG
            Console.WriteLine(xml.ToString());
#endif

            //type name inside XML
            var xmlElementName = xml?.Name;

            //get the value for parsing later
            var rawVal = xml.Value;


            //make sure the XML enclosing type has the same name
            //check both full class name, and short class name
            var isSameName = xmlElementName == typeNameFullName || xmlElementName == typeof(T).GetShortTypeName();

            //if not same name raise error
            if (!isSameName)
            {
                throw new Exception($"Can't parse XML {xmlElementName} to {typeNameFullName}");
            }

            //implements ToXml()
            var typeImplementsToXml = typeof(T).GetInterfaces().Any(x =>
                x.IsGenericType &&
                x.GetGenericTypeDefinition() == typeof(IToXml));

            //type has owm ToXml method
            if (typeImplementsToXml)
            {
                dynamic inputTypeInstance = GetInstance(typeof(T).FullName);

                return inputTypeInstance.FromXml(xml);

            }

            //if type is an Enum process differently
            if (typeof(T).IsEnum)
            {
                var parsedEnum = (T)Enum.Parse(typeof(T), rawVal);

                return parsedEnum;
            }

            //else it is a value type
            if (typeof(T) == typeof(string))
            {
                return rawVal;
            }

            if (typeof(T) == typeof(double))
            {
                return Double.Parse(rawVal);
            }

            if (typeof(T) == typeof(int))
            {
                return int.Parse(rawVal);
            }

            //raise error since converter not implemented
            throw new NotImplementedException($"XML converter for {typeNameFullName}, not implemented!");
        }

        /// <summary>
        /// Gets only the name of the Class, without assembly
        /// </summary>
        public static string GetShortTypeName(this Type type)
        {
            var sb = new StringBuilder();
            var name = type.Name;
            if (!type.IsGenericType) return name;
            sb.Append(name.Substring(0, name.IndexOf('`')));
            sb.Append("<");
            sb.Append(string.Join(", ", type.GetGenericArguments()
                .Select(t => t.GetShortTypeName())));
            sb.Append(">");
            return sb.ToString();
        }

        public static bool Implements<I>(this Type type, I @interface) where I : class
        {
            if (((@interface as Type) == null) || !(@interface as Type).IsInterface)
                throw new ArgumentException("Only interfaces can be 'implemented'.");

            return (@interface as Type).IsAssignableFrom(type);
        }

        /// <summary>
        /// For converting value types, String, Double, etc.
        /// </summary>
        //public static dynamic XmlToValueType<T>(XElement xml) 
        //{
        //    //get the name of the Enum
        //    var typeName = nameof(T);


        //    //raise error since not XML type and Input type mismatch
        //    throw new Exception($"Can't parse XML to {typeName}");
        //}


        /// <summary>
        /// Gets an instance of Class from string name
        /// </summary>
        public static object GetInstance(string strFullyQualifiedName)
        {
            Type type = Type.GetType(strFullyQualifiedName);
            if (type != null)
                return Activator.CreateInstance(type);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(strFullyQualifiedName);
                if (type != null)
                    return Activator.CreateInstance(type);
            }

            return null;
        }



        /// <summary>
        /// Converts days to hours
        /// </summary>
        /// <returns></returns>
        public static double DaysToHours(double days) => days * 24.0;

        public static double MinutesToHours(double minutes) => minutes / 60.0;

        public static double MinutesToYears(double minutes) => minutes / 525600.0;

        public static double MinutesToDays(double minutes) => minutes / 1440.0;

        /// <summary>
        /// Given a date it will count the days to the end of that year
        /// </summary>
        public static double GetDaysToNextYear(Time getBirthDateTime)
        {
            //get start of next year
            var standardTime = getBirthDateTime.GetStdDateTimeOffset();
            var nextYear = standardTime.Year + 1;
            var startOfNextYear = new DateTimeOffset(nextYear, 1, 1, 0, 0, 0, 0, standardTime.Offset);

            //calculate difference of days between 2 dates
            var diffDays = (startOfNextYear - standardTime).TotalDays;

            return diffDays;
        }

        /// <summary>
        /// Gets the time now in the system in text form
        /// formatted with standard style (HH:mm dd/MM/yyyy zzz) 
        /// </summary>
        public static string GetNowSystemTimeText() => DateTimeOffset.Now.ToString(Time.DateTimeFormat);
        /// <summary>
        /// Gets the time now in the system in text form with seconds (HH:mm:ss dd/MM/yyyy zzz) 
        /// </summary>
        public static string GetNowSystemTimeSecondsText() => DateTimeOffset.Now.ToString(Time.DateTimeFormatSeconds);

        /// <summary>
        /// Gets the time now in the Server (+8:00) in text form with seconds (HH:mm:ss dd/MM/yyyy zzz) 
        /// </summary>
        public static string GetNowServerTimeSecondsText() => DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(8)).ToString(Time.DateTimeFormatSeconds);

        /// <summary>
        /// Custom hash generator for Strings. Returns consistent/deterministic values
        /// If null returns 0
        /// Note: MD5 (System.Security.Cryptography) not used because not supported in Blazor WASM
        /// </summary>
        public static int GetStringHashCode(string stringToHash)
        {
            if (stringToHash == null)
            {
                return 0;
            }

            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < stringToHash.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ stringToHash[i];
                    if (i == stringToHash.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ stringToHash[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }


            //MD5 md5Hasher = MD5.Create();
            //var hashedByte = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(stringToHash));
            //return BitConverter.ToInt32(hashedByte, 0);

        }

        /// <summary>
        /// Gets random unique ID
        /// </summary>
        public static string GenerateId() => Guid.NewGuid().ToString("N");


        /// <summary>
        /// Converts any list to comma separated string
        /// Note: calls ToString();
        /// </summary>
        public static string ListToString<T>(List<T> list)
        {
            var combinedNames = "";
            foreach (var item in list)
            {
                combinedNames += item.ToString() + ", ";
            }

            return combinedNames;
        }


        //█▀▀ █░█ ▀▀█▀▀ █▀▀ █▀▀▄ █▀▀ ░▀░ █▀▀█ █▀▀▄ 　 █▀▄▀█ █▀▀ ▀▀█▀▀ █░░█ █▀▀█ █▀▀▄ █▀▀ 
        //█▀▀ ▄▀▄ ░░█░░ █▀▀ █░░█ ▀▀█ ▀█▀ █░░█ █░░█ 　 █░▀░█ █▀▀ ░░█░░ █▀▀█ █░░█ █░░█ ▀▀█ 
        //▀▀▀ ▀░▀ ░░▀░░ ▀▀▀ ▀░░▀ ▀▀▀ ▀▀▀ ▀▀▀▀ ▀░░▀ 　 ▀░░░▀ ▀▀▀ ░░▀░░ ▀░░▀ ▀▀▀▀ ▀▀▀░ ▀▀▀


        /// <summary>
        /// Find the first offset in the string that might contain the characters
        /// in `needle`, in any order. Returns -1 if not found.
        /// <para>This function can return false positives</para>
        /// </summary>
        public static bool FindCluster(this string? haystack, string? needle)
        {
            if (haystack == null) return false;
            if (needle == null) return false;

            if (haystack.Length < needle.Length) return false;

            long sum = needle.ToCharArray().Sum(c => c);
            long rolling = haystack.ToCharArray().Take(needle.Length).Sum(c => c);

            var idx = 0;
            var head = needle.Length;
            while (rolling != sum)
            {
                if (head >= haystack.Length) return false;
                rolling -= haystack[idx];
                rolling += haystack[head];
                head++;
                idx++;
            }

            return true;
        }

        /// <summary>
        /// Remap from 1 range to another
        /// </summary>
        public static float Remap(this float from, float fromMin, float fromMax, float toMin, float toMax)
        {
            var fromAbs = from - fromMin;
            var fromMaxAbs = fromMax - fromMin;

            var normal = fromAbs / fromMaxAbs;

            var toMaxAbs = toMax - toMin;
            var toAbs = toMaxAbs * normal;

            var to = toAbs + toMin;

            return to;
        }

        /// <summary>
        /// Remap from 1 range to another
        /// </summary>
        public static double Remap(this double from, double fromMin, double fromMax, double toMin, double toMax)
        {
            var fromAbs = from - fromMin;
            var fromMaxAbs = fromMax - fromMin;

            var normal = fromAbs / fromMaxAbs;

            var toMaxAbs = toMax - toMin;
            var toAbs = toMaxAbs * normal;

            var to = toAbs + toMin;

            return to;
        }

        /// <summary>
        /// Gets public IP address of client sending the http request
        /// </summary>
        public static IPAddress GetCallerIp(this HttpRequestMessage request)
        {
            IPAddress result = null;
            if (request.Headers.TryGetValues("X-Forwarded-For", out IEnumerable<string> values))
            {
                var ipn = values.FirstOrDefault().Split(new char[] { ',' }).FirstOrDefault().Split(new char[] { ':' }).FirstOrDefault();
                IPAddress.TryParse(ipn, out result);
            }
            return result;
        }

        public static string StreamToString(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            string text = reader.ReadToEnd();

            return text;
        }

        /// <summary>
        /// Checks if result xml sent from api to client has status pass
        /// else will return false, will also false if any error in result
        /// </summary>
        public static bool IsResultPass(XElement result)
        {
            try
            {
                return result.Element("Status")?.Value == "Pass";
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Input the result xml obtained from API
        /// Gets first child element in "Payload" element
        /// </summary>
        public static XElement? GetPayload(XElement result)
        {
            try
            {
                return result.Element("Payload")?.Elements()?.FirstOrDefault();

            }
            catch (Exception) { return new XElement("PayloadFailed"); }
        }

        /// <summary>
        /// Converts a timezone (+08:00) in string form to parsed timespan 
        /// </summary>
        public static TimeSpan StringToTimezone(string timezoneRaw)
        {
            return DateTimeOffset.ParseExact(timezoneRaw, "zzz", CultureInfo.InvariantCulture).Offset;
        }

        /// <summary>
        /// Returns system timezone offset as TimeSpan
        /// </summary>
        public static string GetSystemTimezoneStr() => DateTimeOffset.Now.ToString("zzz");

        /// <summary>
        /// Returns system timezone offset as TimeSpan
        /// </summary>
        public static TimeSpan GetSystemTimezone() => DateTimeOffset.Now.Offset;

        public static async Task<(string FullName, double Latitude, double Longitude)> AddressToCoordinate(string address)
        {
            //create the request url for Google API
            var apiKey = "AIzaSyDqBWCqzU1BJenneravNabDUGIHotMBsgE";
            var url = $"https://maps.googleapis.com/maps/api/geocode/xml?key={apiKey}&address={Uri.EscapeDataString(address)}&sensor=false";

            //get location data from GoogleAPI
            var rawReplyXml = await ReadFromServerXmlReply(url);

#if DEBUG
            Console.WriteLine(rawReplyXml.ToString());
#endif

            //extract out the longitude & latitude
            var locationData = new XDocument(rawReplyXml);
            var result = locationData.Element("GeocodeResponse")?.Element("result");
            var locationElement = result?.Element("geometry")?.Element("location");
            var lat = double.Parse(locationElement?.Element("lat")?.Value ?? "0");
            var lng = double.Parse(locationElement?.Element("lng")?.Value ?? "0");

            //round coordinates to 3 decimal places
            lat = Math.Round(lat, 3);
            lng = Math.Round(lng, 3);

            //get full name with country & state
            var fullName = result?.Element("formatted_address")?.Value;

            return (FullName: fullName, Latitude: lat, Longitude: lng);

        }

        /// <summary>
        /// gets the name of the place given th coordinates, uses Google API
        /// </summary>
        public static async Task<string> CoordinateToAddress(decimal longitude, decimal latitude, string apiKey)
        {
            //create the request url for Google API
            var url = string.Format($"https://maps.googleapis.com/maps/api/geocode/xml?latlng={latitude},{longitude}&key={apiKey}");

            //get location data from GoogleAPI
            var rawReplyXml = await ReadFromServerXmlReply(url);

            //extract out the longitude & latitude
            var locationData = new XDocument(rawReplyXml);
            var localityResult = locationData.Element("GeocodeResponse")?.Elements("result").FirstOrDefault(result => result.Element("type")?.Value == "locality");
            var locationName = localityResult?.Element("formatted_address")?.Value;


            return locationName;

        }

        /// <summary>
        /// Given a location & time, will use Google Timezone API
        /// to get accurate time zone that was/is used
        /// Must input valid geo location 
        /// NOTE:
        /// - offset of timeAtLocation not important
        /// - googleGeoLocationApiKey needed to work
        /// </summary>
        public static async Task<TimeSpan> GetTimezoneOffset(string locationName, DateTimeOffset timeAtLocation, string apiKey)
        {
            //get geo location first then call underlying method
            var geoLocation = await GeoLocation.FromName(locationName);
            return Tools.StringToTimezone(await GetTimezoneOffset(geoLocation, timeAtLocation, apiKey));
        }
        public static async Task<string> GetTimezoneOffsetString(string locationName, DateTime timeAtLocation, string apiKey)
        {
            //get geo location first then call underlying method
            var geoLocation = await GeoLocation.FromName(locationName);
            return await GetTimezoneOffset(geoLocation, timeAtLocation, apiKey);
        }
        public static async Task<string> GetTimezoneOffsetString(string location, string dateTime)
        {
            //get timezone from Google API
            var lifeEvtTimeNoTimezone = DateTime.ParseExact(dateTime, Time.DateTimeFormatNoTimezone, null);
            var timezone = await Tools.GetTimezoneOffsetString(location, lifeEvtTimeNoTimezone, "AIzaSyDqBWCqzU1BJenneravNabDUGIHotMBsgE");

            return timezone;

            //get start time of life event and find the position of it in slices (same as now line)
            //so that this life event line can be placed exactly on the report where it happened
            //var lifeEvtTimeStr = $"{dateTime} {timezone}"; //add offset 0 only for parsing, not used by API to get timezone
            //var lifeEvtTime = DateTimeOffset.ParseExact(lifeEvtTimeStr, Time.DateTimeFormat, null);

            //return lifeEvtTime;
        }


        /// <summary>
        /// Given a location & time, will use Google Timezone API
        /// to get accurate time zone that was/is used
        /// NOTE:
        /// - offset of timeAtLocation not important
        /// - googleGeoLocationApiKey needed to work
        /// </summary>
        public static async Task<string> GetTimezoneOffset(GeoLocation geoLocation, DateTimeOffset timeAtLocation, string apiKey)
        {
            //use timestamp to account for historic timezone changes
            var locationTimeUnix = timeAtLocation.ToUnixTimeSeconds();
            var longitude = geoLocation.GetLongitude();
            var latitude = geoLocation.GetLatitude();

            //create the request url for Google API 
            //get the API key string stored separately (for security reasons)
            var url = string.Format($@"https://maps.googleapis.com/maps/api/timezone/xml?location={latitude},{longitude}&timestamp={locationTimeUnix}&key={apiKey}");


            //get location data from GoogleAPI
            //< TimeZoneResponse >
            //  < status > OK </ status >
            //  < raw_offset > 28800.0000000 </ raw_offset >
            //  < dst_offset > 0.0000000 </ dst_offset >
            //  < time_zone_id > Asia / Kuala_Lumpur </ time_zone_id >
            //  < time_zone_name > Malaysia Time </ time_zone_name >
            //</ TimeZoneResponse >
            var timeZoneResponseXml = await ReadFromServerXmlReply(url);

            //extract out the timezone offset
            var offsetSeconds = double.Parse(timeZoneResponseXml?.Element("raw_offset")?.Value);
            //offset needs to be "whole" minutes, else fail
            //purposely hard cast to int to remove not whole minutes
            var notWhole = TimeSpan.FromSeconds(offsetSeconds).TotalMinutes;
            var offsetMinutes = TimeSpan.FromMinutes((int)Math.Round(notWhole));
            var parsedOffsetString = DateTimeOffset.UtcNow.ToOffset(offsetMinutes).ToString("zzz");

            return parsedOffsetString;

        }

        /// <summary>
        /// Calls a URL and returns the content of the result as XML
        /// Even if content is returned as JSON, it is converted to XML
        /// Note: if JSON auto adds "Root" as first element, unless specified
        /// for XML data root element name is ignored
        /// </summary>
        public static async Task<XElement> ReadFromServerXmlReply(string apiUrl, string rootElementName = "Root")
        {

            string rawMessage = "";

            try
            {
                //send request to API server
                var result = await RequestServer(apiUrl);

                //parse data reply
                rawMessage = result.Content.ReadAsStringAsync().Result;

                //raw message can be JSON or XML
                //try parse as XML if fail then as JSON
                var readFromServerXmlReply = XElement.Parse(rawMessage);

                return readFromServerXmlReply;
            }
            catch (Exception)
            {
                //try to parse data as JSON
                try
                {
                    var rawXml = JsonConvert.DeserializeXmlNode(rawMessage, rootElementName);
                    var readFromServerXmlReply = XElement.Parse(rawXml?.InnerXml ?? "<Empty/>");

                    return readFromServerXmlReply;
                }
                //unparseable data, let user know
                catch (Exception e)
                {
                    throw new Exception($"ReadFromServerXmlReply()\n{rawMessage}", e);
                }
            }



            // FUNCTIONS

            async Task<HttpResponseMessage> RequestServer(string receiverAddress)
            {
                //prepare the data to be sent
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, receiverAddress);

                //get the data sender
                using var client = new HttpClient();

                //tell sender to wait for complete reply before exiting
                var waitForContent = HttpCompletionOption.ResponseContentRead;

                //send the data on its way
                var response = await client.SendAsync(httpRequestMessage, waitForContent);

                //return the raw reply to caller
                return response;
            }
        }

        /// <summary>
        /// Given a list of strings will return one by random
        /// Used to make dynamic user error & info messages
        /// </summary>
        public static string RandomSelect(string[] msgList)
        {
            // Create a Random object  
            Random rand = new Random();

            // Generate a random index less than the size of the array.  
            int randomIndexNumber = rand.Next(msgList.Length);

            //return random text from list to caller
            return msgList[randomIndexNumber];
        }

        /// <summary>
        /// Split string by character count
        /// </summary>
        public static IEnumerable<string> SplitByCharCount(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
    }

}
