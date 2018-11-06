using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UpdateSDMXConsole
{
    class Program
    {
        static string endpointGame = ConfigurationManager.AppSettings["endpointGame"];
        static string indicatorCode = ConfigurationManager.AppSettings["indicatorCode"];
        static string endURL = ConfigurationManager.AppSettings["endURL"];
        static string featureServiceURL = ConfigurationManager.AppSettings["serviceURL"];
        static string baseURL = ConfigurationManager.AppSettings["baseURL"];
        static string username = ConfigurationManager.AppSettings["pUsername"];
        static string password = ConfigurationManager.AppSettings["pPassword"];
        static string definitionTitle = ConfigurationManager.AppSettings["definitionTitle"];
        static string refAreaName = ConfigurationManager.AppSettings["refAreaName"];
        static string firstOBSValue = ConfigurationManager.AppSettings["firstOBSValue"];
        static string lastOBSValue = ConfigurationManager.AppSettings["lastOBSValue"];
        static string genericNSP = ConfigurationManager.AppSettings["genericNSP"];
        static DataClasses1DataContext ddc = new DataClasses1DataContext();
        static Result responze = new Result();

        static void Main(string[] args)
        {
            Console.Title = "Update SDMX Service";
            Console.WriteLine("Wait while Update Process ....");
            Thread.Sleep(5000);

            //Begin Process
            
            //Now POST features onto the Indicator SDMX feature service
            //Get the Portal in question
            AGOL currentPortal = new AGOL(username, password);
            var tokenz = currentPortal.Token;

            overwriteService(tokenz, "Service Definition", definitionTitle);
            Library.WriteErrorLog("Timer ticked and something was done");
            string Result = "";
            string endpointUrl = endpointGame + indicatorCode + endURL;

            XNamespace generic = genericNSP;


            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpointUrl);
                Library.WriteErrorLog(request.RequestUri.ToString());


                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                Encoding enc = System.Text.Encoding.GetEncoding(1252);
                StreamReader loResponseStream = new
                    StreamReader(response.GetResponseStream(), enc);

                //string Result = loResponseStream.ReadToEnd();
                Result = loResponseStream.ReadToEnd();

                XDocument xdocument = XDocument.Parse(Result);
                IEnumerable<XElement> employees = xdocument.Elements();
                foreach (var employee in employees)
                {
                    Library.WriteErrorLog("next Up!!");
                    var descendants = xdocument.Descendants(generic + "Series");

                    XElement tempElement = xdocument.Descendants(generic + "Series").FirstOrDefault();

                    foreach (var item in descendants)
                    {
                        Console.WriteLine(item.Value);
                        XDocument itemXML = XDocument.Parse(item.ToString());
                        var itemDescendants = itemXML.Descendants(generic + "Value");
                        var obsDescendants = itemXML.Descendants(generic + "ObsDimension");
                        var obsDescendVal = itemXML.Descendants(generic + "ObsValue");

                        string arrayFeatureJSON = "[{";
                        string featureJSON = "'attributes': {";
                        string initialFeature = "";
                        int featureEnd = 0;
                        int obsCount = 0;
                        int seriesItemCount = 0;
                        double? coordinateX = 0.0078;
                        double? coordinateY = 0.0089;



                        foreach (var seriesItem in itemDescendants)
                        {
                            Console.WriteLine(seriesItem.FirstAttribute.Value);
                            XAttribute hj = seriesItem.LastAttribute;
                            seriesItemCount++;

                            //Build Initial part of feature
                            if (seriesItem.FirstAttribute.Value == refAreaName)
                            {
                                featureJSON += "'" + seriesItem.FirstAttribute.Value + "': " + seriesItem.LastAttribute.Value + ",";

                                //Get geometry of Reference Area
                                coordinateX = ddc.GeoAreas.Where(v => v.GeoAreaCode.Equals(int.Parse(seriesItem.LastAttribute.Value))).First().Longitude;
                                coordinateY = ddc.GeoAreas.Where(v => v.GeoAreaCode.Equals(int.Parse(seriesItem.LastAttribute.Value))).First().Latitude;
                            }
                            else
                            {
                                if (seriesItem.FirstAttribute.Value == firstOBSValue)
                                {
                                    featureEnd++;
                                    if (featureEnd == 1 && initialFeature == "")
                                    {
                                        initialFeature = featureJSON;
                                    }
                                    else if (featureEnd == 2)
                                    {
                                        //Add OBS Value and OBS Dimension values
                                        string preceedingFeatureJSON = featureJSON.Substring(featureJSON.Length - 1, 1);
                                        if (preceedingFeatureJSON != ",")
                                        {
                                            featureJSON += ",'" + obsDescendants.ToList()[obsCount].FirstAttribute.Value + "': " + "'" + obsDescendants.ToList()[obsCount].LastAttribute.Value + "'";
                                        }
                                        else
                                        {
                                            featureJSON += "'" + obsDescendants.ToList()[obsCount].FirstAttribute.Value + "': " + "'" + obsDescendants.ToList()[obsCount].LastAttribute.Value + "'";
                                        }
                                        featureJSON += ",'" + "ObsValue" + "': " + obsDescendVal.ToList()[obsCount].FirstAttribute.Value;
                                        obsCount++;

                                        featureJSON += "}, 'geometry' : {'x' : " + coordinateX.ToString() + ", 'y' : " + coordinateY + "}}";
                                        if (arrayFeatureJSON.Length < 20)
                                        {
                                            arrayFeatureJSON += featureJSON;
                                        }
                                        else
                                        {
                                            arrayFeatureJSON += ", {" + featureJSON;
                                        }
                                        featureJSON = initialFeature;
                                        featureEnd = 1;
                                    }

                                }


                                string output = arrayFeatureJSON.Substring(arrayFeatureJSON.Length - 1, 1);
                                string lastFeatureJSON = featureJSON.Substring(featureJSON.Length - 1, 1);

                                if (output == "{")
                                {
                                    featureJSON += "'" + seriesItem.FirstAttribute.Value + "': " + "'" + seriesItem.LastAttribute.Value + "'" + ",";
                                }
                                else
                                {
                                    if (lastFeatureJSON != ",")
                                    {
                                        featureJSON += ",'" + seriesItem.FirstAttribute.Value + "': " + "'" + seriesItem.LastAttribute.Value + "'";
                                    }
                                    else
                                    {
                                        featureJSON += "'" + seriesItem.FirstAttribute.Value + "': " + "'" + seriesItem.LastAttribute.Value + "'";
                                    }
                                }

                                if (seriesItem.FirstAttribute.Value == lastOBSValue)
                                {
                                    if (obsCount == (obsDescendants.Count() - 1))
                                    {

                                        //Add OBS Value and OBS Dimension values
                                        featureJSON += ",'" + obsDescendants.ToList()[obsCount].FirstAttribute.Value + "': " + "'" + obsDescendants.ToList()[obsCount].LastAttribute.Value + "'";
                                        featureJSON += ",'" + "ObsValue" + "': " + "'" + obsDescendVal.ToList()[obsCount].FirstAttribute.Value + "'";
                                        obsCount++;

                                        featureJSON += "}, 'geometry' : {'x' : " + coordinateX.ToString() + ", 'y' : " + coordinateY + "}}";
                                        if (arrayFeatureJSON.Length < 20)
                                        {
                                            arrayFeatureJSON += featureJSON;
                                        }
                                        else
                                        {
                                            arrayFeatureJSON += ", {" + featureJSON;
                                        }
                                        featureJSON = initialFeature;
                                        featureEnd = 1;
                                    }
                                }

                            }

                        }

                        arrayFeatureJSON += "]";


                        //define the URL for querying the feature service based on the plot number
                        var queryURL = featureServiceURL + "/query";

                        //define the URL for updating a feature in the Feature Service
                        var updateURL = featureServiceURL + "/addFeatures";

                        //Create Features Name Value Collection
                        var updateData = new NameValueCollection();
                        updateData["token"] = tokenz;
                        updateData["f"] = "json";

                        updateData["features"] = arrayFeatureJSON;

                        Library.WriteErrorLog(arrayFeatureJSON);

                        string updateResult = @getQueryResponse(updateData, updateURL);

                        Thread.Sleep(5000);

                        //Report back on the feature - whether updated or not
                        var uObj = JsonConvert.DeserializeObject(updateResult) as JObject;

                        if ((JArray)uObj["updateResults"] != null)
                        {
                            JArray ufeatures = (JArray)uObj["updateResults"];

                            string boolResult = ((JObject)ufeatures[0])["success"].ToString();

                            responze.resultText = updateResult;

                            if (boolResult == "True")
                            {
                                responze.resultCode = "Result Code: 0000";
                                responze.resultText = "OK! Feature number " + ((JObject)ufeatures[0])["objectId"].ToString() + " successfully updated";
                            }
                            else
                            {
                                responze.resultCode = "Result Code: 0001";
                                responze.resultText = updateResult + ". Contact Administrator";
                            }

                        }
                        else
                        {
                            responze.resultCode = "Result Code: 0003";
                            responze.resultText = updateResult + ". Contact Administrator";
                        }
                    }



                }

                loResponseStream.Close();
                response.Close();

                //Response.Write(Result);
            }
            catch (WebException ex)
            {
                Library.WriteErrorLog(ex);
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                if (response != null)
                {
                    StreamReader errorResponseStream = new StreamReader(response.GetResponseStream());

                    //string Result = errorResponseStream.ReadToEnd();
                    Result = errorResponseStream.ReadToEnd();
                    //Response.Write(Result);
                }
            }
            
        }

        private static void overwriteService(string token, string itemType, string title)
        {
            string itemID;
            string searchURL = baseURL + "/search";

            string query_dict = "{'f': 'json','token':" + token + ", 'q': title:\"" + title + "\"AND owner:\"" +
                      username + "\" AND type:\"" + itemType + "\"" + "}";

            var updateData = new NameValueCollection();
            updateData["token"] = token;
            updateData["f"] = "json";
            updateData["q"] = "title:\"" + title + "\"AND owner:\"" + username + "\" AND type:\"" + itemType + "\"";

            string updateResult = @getQueryResponse(updateData, searchURL);

            //Report back on the feature - whether updated or not
            var uObj = JsonConvert.DeserializeObject(updateResult) as JObject;

            var thngio = uObj.Children()["results"].Children();



            if ((JArray)uObj["results"] != null)
            {
                JArray ufeatures = (JArray)uObj["results"];
                itemID = ufeatures[0]["id"].ToString();

                //Proceed to publish
                //Publish the service based on the definition file
                string publishURL = baseURL + "/content/users/" + username + "/publish";
                var overwriteData = new NameValueCollection();
                overwriteData["token"] = token;
                overwriteData["f"] = "json";
                overwriteData["filetype"] = "serviceDefinition";
                overwriteData["itemID"] = itemID;
                overwriteData["overwrite"] = "true";

                string overwriteResult = @getOverwriteResponse(overwriteData, publishURL);

                Console.WriteLine("Feature Service has been overwritten");
            }
            else
            {
                Console.WriteLine("Error overwriting " + title + " Feature Service!!!! Definition file not found");
            }


        }

        private static string getQueryResponse(NameValueCollection qData, string v)
        {
            string responseData;
            var webClient = new System.Net.WebClient();
            var response = webClient.UploadValues(v, qData);
            responseData = System.Text.Encoding.UTF8.GetString(response);
            return responseData;
        }

        private static string getOverwriteResponse(NameValueCollection overwriteData, string publishURL)
        {
            string responseData;
            var webClient = new System.Net.WebClient();
            var response = webClient.UploadValues(publishURL, overwriteData);
            responseData = System.Text.Encoding.UTF8.GetString(response);

            Thread.Sleep(60000);
            return responseData;
        }
    }
}
