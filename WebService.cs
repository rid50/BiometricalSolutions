using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
//using System.Data.SqlClient;
using System.Data;
using System.Data.SqlTypes;
//using System.Data.SqlServerCe;
using System.Data.Common;
using System.Data.SqlClient;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Net;
using System.IO;

namespace DataSourceServices
{
    class WebService : DataSource
    {
        //string siteUrl = "http://nomad.host22.com/";
        string siteUrl = "http://localhost/";

        public override byte[] GetImage(IMAGE_TYPE imageType, int id)
        {
            String url;
            if (imageType == IMAGE_TYPE.picture)
                url = siteUrl + "kuwaitindex/bio_picture.php?id=";
            else
                url = siteUrl + "kuwaitindex/bio_wsq.php?id=";

            url += id.ToString();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";

            byte[] bytes = null;
            using (Stream sm = request.GetResponse().GetResponseStream())
            {
                try
                {
                    //List<JsonResult> result = jsonStr.FromJson<List<JsonResult>>(s);

                    //StreamReader sr = new StreamReader(sm);
                    //String str = sr.ReadToEnd();
                    //sr.Close();
                    DataContractJsonSerializer serialiser = new DataContractJsonSerializer(typeof(List<JsonResult>));
                    List<JsonResult> result = serialiser.ReadObject(sm) as List<JsonResult>;
                    if (result.Count != 0)
                    {
                        if (result[0].result != null && result[0].result != "success")
                            throw new Exception(result[0].result);
                        //MessageBox.Show(result[0].result);
                        else
                        {
                            try
                            {
                                if (imageType == IMAGE_TYPE.picture)
                                {
                                    if (result[0].picture != null)
                                        bytes = System.Convert.FromBase64String(result[0].picture);
                                }
                                else
                                {
                                    if (result[0].wsq != null)
                                        bytes = System.Convert.FromBase64String(result[0].wsq);
                                }
                            }
                            catch (Exception ex) { throw new Exception(ex.Message); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
            return bytes;
        }
    }

    [DataContract]
    class JsonResult
    {
#pragma warning disable 0649    //warning CS0649: Field 'DataSourceServices.JsonResult.result' is never assigned to, and will always have its default value null
        [DataMember(Name = "result", IsRequired = false)]
        public string result;
        [DataMember(Name = "picture", IsRequired = false)]
        public string picture;
        [DataMember(Name = "wsq", IsRequired = false)]
        public string wsq;
#pragma warning restore 0649
    }
}
