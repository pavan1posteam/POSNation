using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POS_Nation.Models;

namespace POS_Nation
{
    class Program
    {
        static void Main(string[] args)
        {
            string DeveloperId = ConfigurationManager.AppSettings["DeveloperId"];
            try
            {
                POSSettings pOSSettings = new POSSettings();
                pOSSettings.IntializeStoreSettings();
                foreach (POSSetting current in pOSSettings.PosDetails)
                {
                    try
                    {
                        var data = GetData(current.StoreSettings.StoreId, current.StoreSettings.POSSettings.Username, current.StoreSettings.POSSettings.Password, current.StoreSettings.POSSettings.AuthUrl, current.StoreSettings.POSSettings.ItemUrl, current.StoreSettings.POSSettings.FtpUserName, current.StoreSettings.POSSettings.FtpPassword);
                        var jObj = (JObject.Parse(data)["data"]);
                        Dictionary<object, object> dictObj = jObj.ToObject<Dictionary<object, object>>();
                        var itemsObj = dictObj.ToList().Select(s => s.Value).ToList();
                        var itemList = new List<Item>();

                        foreach (var item in itemsObj)
                        {
                            itemList.Add(JsonConvert.DeserializeObject<Item>(item.ToString()));
                        }
                        POSNationcsvConverter(itemList, current.StoreSettings.StoreId, current.StoreSettings.POSSettings.tax, current.StoreSettings.POSSettings.FtpUserName, current.StoreSettings.POSSettings.FtpPassword);
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        new clsEmail().sendEmail(DeveloperId, "", "", "Error in POSNation@" + DateTime.UtcNow + current.StoreSettings.StoreId + " GMT", ex.Message + "<br/>" + ex.StackTrace);
                    }

                }

            }
            catch (Exception ex)
            {
                new clsEmail().sendEmail(DeveloperId, "", "", "Error in POSNation@" + DateTime.UtcNow + " GMT", ex.Message + "<br/>" + ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
        }
        private static string GetData(int Storeid, string Username, string Password, string AuthUrl, string ItemUrl, string ftpUserName, string ftpPassword)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                var BaseUrl = AuthUrl;
                Password = ComputeSha256Hash(Password);
                var dataObj = JsonConvert.SerializeObject(new { username = Username, password = Password });
                BaseUrl = $"{BaseUrl}?data={dataObj}";
                var responseData = string.Empty;
                HttpClient httpClient = new HttpClient(new HttpClientHandler { UseCookies = true });
                httpClient.BaseAddress = new Uri(BaseUrl);
                httpClient.Timeout = TimeSpan.FromMinutes(30);
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

                HttpResponseMessage response = new HttpResponseMessage();
                Task.Run(async () =>
                {
                    response = await httpClient.PostAsync("", new StringContent(""));
                    responseData = response.Content.ReadAsStringAsync().Result;
                    response.EnsureSuccessStatusCode();
                }).Wait();
                var BaseUrl1 = ItemUrl;
                response = new HttpResponseMessage();
                Task.Run(async () =>
                {
                    response = await httpClient.GetAsync(BaseUrl1);
                    responseData = response.Content.ReadAsStringAsync().Result;
                    response.EnsureSuccessStatusCode();
                }).Wait();
                return responseData;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " " + Storeid);
            }
            return "";
        }      
        private static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        private static void POSNationcsvConverter(List<Item> productList, int storeid, decimal tax, string ftpUserName, string ftpPassword)
        {
            string folderPath = ConfigurationManager.AppSettings.Get("BaseDirectory");
            string IrrespectiveQTY = ConfigurationManager.AppSettings["IrrespectiveQTY"];
            string packfilteration = ConfigurationManager.AppSettings.Get("packfilteration");
            string Deposit = ConfigurationManager.AppSettings.Get("Deposit");
            string UOM_REMOVE = ConfigurationManager.AppSettings.Get("UOM_REMOVE");
            List<Modifier> xmd = new List<Modifier>();

            try
            {
                List<datatableModel> pf = new List<datatableModel>();
                List<FullNameProductModel> pd = new List<FullNameProductModel>();
                foreach (var item in productList)
                {
                    try
                    {
                        datatableModel pdf = new datatableModel();
                        FullNameProductModel fdf = new FullNameProductModel();
                        pdf.StoreID = storeid;
                        pdf.upc = item.code;
                        string abc = pdf.upc;
                        string[] number = abc.Split(',');
                        string up = Regex.Match(number[0], @"^\d+$").ToString();

                        if (!string.IsNullOrEmpty(up))
                        {
                            pdf.upc = '#' + up;
                            fdf.upc = '#' + up;
                            pdf.sku = '#' + up;
                            fdf.sku = '#' + up;
                        }
                        else
                        {
                            continue;
                        }

                        decimal qty = Convert.ToDecimal(item.total_stock);
                        pdf.Qty = Convert.ToInt32(qty) > 0 ? Convert.ToInt32(qty) : 0;
                        pdf.pack = "1";
                        fdf.pack = 1;

                        if (UOM_REMOVE.Contains(storeid.ToString()))
                        {
                            if (!item.description.Contains("12OZ") && !item.description.Contains("16OZ"))
                            {
                                pdf.uom = item.description;
                                fdf.uom = item.description;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            pdf.uom = item.description;
                            fdf.uom = item.description;
                        }
                        pdf.Tax = tax;
                        string b = item.name;
                        if (b.Contains("\n"))
                        {
                            b = b.Replace("\n", String.Empty);
                        }

                        pdf.StoreProductName = b;
                        fdf.pname = b;
                        string a = item.name;
                        if (a.Contains("\n"))
                        {
                            a = a.Replace("\n", String.Empty);
                        }
                        pdf.StoreDescription = a.Trim();
                        fdf.pdesc = a.Trim();
                        pdf.Price = Convert.ToDecimal(item.price);
                        fdf.Price = Convert.ToDecimal(item.price);
                        if (pdf.Price <= 0 || fdf.Price <= 0)
                        {
                            continue;
                        }
                        pdf.Start = "";
                        pdf.End = "";
                        //fdf.pcat = item.cat_group_name.ToString().Trim();
                        fdf.pcat = item.category_name.ToString().Trim();
                        if (fdf.pcat == "CIGARS" || fdf.pcat == "NON UPC CIGARS" || fdf.pcat == "ALLOCATED BOURBON" || fdf.pcat == "ALLOCATED" || fdf.pcat == "SEMI ALLOCATED BOURBON")
                        {
                            continue;
                        }
                        pdf.Tax = tax;
                        if (storeid == 12256)// #38687
                        {
                            fdf.pcat = item.cat_group_name.ToString().Trim();
                            fdf.pcat1 = item.category_name.ToString().Trim();
                            if (fdf.pcat.ToLower().Contains("wine") || fdf.pcat.ToLower().Contains("liquor") || fdf.pcat1.ToLower().Contains("liquor") || fdf.pcat1.ToLower().Contains("liquor"))
                            {
                                pdf.Tax = 0;
                            }
                            else
                            {
                                pdf.Tax = tax;
                            }
                            if ((fdf.pcat.ToLower().Contains("beer") || fdf.pcat1.ToLower().Contains("beer"))&& pdf.Qty == 1)
                            {
                                continue;
                            }
                            if (item.showtoweb == "0")
                            {
                                continue;
                            }
                        }
                        else
                        {
                            fdf.pcat1 = "";
                        }
                        
                        fdf.pcat2 = "";
                        fdf.country = "";
                        fdf.region = "";
                        if (number.Length == 2)
                        {
                            pdf.altupc1 = '#' + number.ElementAt(1);
                        }
                        else if (number.Length == 3)
                        {
                            pdf.altupc1 = '#' + number.ElementAt(1);
                            pdf.altupc2 = '#' + number.ElementAt(2);
                        }
                        else if (number.Length == 4)
                        {
                            pdf.altupc1 = '#' + number.ElementAt(1);
                            pdf.altupc2 = '#' + number.ElementAt(2);
                            pdf.altupc3 = '#' + number.ElementAt(3);
                        }
                        if (item.modifiers.Count > 0 && packfilteration.Contains(storeid.ToString()))
                        {
                            for (int i = 0; i < item.modifiers.Count; i++)
                            {
                                Modifier modifiers = JsonConvert.DeserializeObject<Modifier>(item.modifiers[i].ToString());
                                modifiers.upc = pdf.upc;
                                modifiers.actualqty = Convert.ToInt32(Math.Floor(pdf.Qty / Convert.ToDecimal(modifiers.qty)));
                                xmd.Add(modifiers);
                            }
                        }


                        if (Deposit.Contains(storeid.ToString()) && item.itemdeposit != null)
                        {
                            var itemDeposits = item.itemdeposit as List<Itemdeposit> ?? new List<Itemdeposit>();

                            // Convert JToken to List<Itemdeposit> if it's not null

                            // Ensure itemDeposits is not null and has items
                            if (itemDeposits != null && itemDeposits.Any())
                            {
                                foreach (var deposit in itemDeposits)
                                {
                                    if (!string.IsNullOrEmpty(deposit.name))
                                    {
                                        if (pdf.uom.Contains("PK"))
                                        {
                                            pdf.uom = deposit.name;
                                            fdf.uom = deposit.name;
                                        }
                                        Regex regex = new Regex(@"\d+");
                                        Match match = regex.Match(deposit.name);
                                        if (match.Success)
                                        {
                                            decimal num = Convert.ToDecimal(match.Value);
                                            pdf.pack = Convert.ToString(num);
                                            fdf.pack = Convert.ToInt32(num);
                                            pdf.deposit = 0.05m * num;
                                        }
                                    }
                                }
                            }
                        }
                        if (storeid == 12061)
                        {
                            if (pdf.Price.ToString().Contains(".97"))
                            {
                                pdf.discount = 1;
                            }
                            else
                            {
                                pdf.discount = 0;
                            }
                        }

                        if (IrrespectiveQTY.Contains(storeid.ToString()))
                        {
                            pf.Add(pdf);
                            pd.Add(fdf);
                        }
                        else
                        {
                            if (pdf.Qty > 0)
                            {
                                pf.Add(pdf);
                                pd.Add(fdf);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                if (packfilteration.Contains(storeid.ToString()))
                {
                    if (IrrespectiveQTY.Contains(storeid.ToString()))
                        xmd.RemoveAll(a => a.actualqty <= 0);
                    var xx = (from p in xmd
                              join m in pf on p.upc equals m.upc
                              select new datatableModel
                              {
                                  StoreID = storeid,
                                  upc = m.upc,
                                  sku = m.sku,
                                  Qty = Math.Floor(m.Qty / Convert.ToDecimal(p.qty)),
                                  pack = p.qty,
                                  uom = m.uom,
                                  Tax = m.Tax,
                                  StoreDescription = m.StoreDescription,
                                  StoreProductName = m.StoreProductName,
                                  Price = Convert.ToDecimal(p.price),
                                  deposit = m.deposit,
                                  sprice = m.sprice,
                                  Start = m.Start,
                                  End = m.End,
                                  altupc1 = m.altupc1,
                                  altupc2 = m.altupc2,
                                  altupc3 = m.altupc3,
                                  altupc4 = m.altupc4,
                                  altupc5 = m.altupc5
                              }).ToList();
                    pf.AddRange(xx);
                    pf = pf.OrderBy(x => long.Parse(x.sku.Substring(1))).ToList();
                    var yy = (from p in xmd
                              join m in pd on p.upc equals m.upc
                              select new FullNameProductModel
                              {
                                  pname = m.pname,
                                  pdesc = m.pname,
                                  upc = m.upc,
                                  sku = m.sku,
                                  pack = Convert.ToInt32(Convert.ToDecimal(p.qty)),
                                  pcat = m.pcat,
                                  pcat1 = m.pcat1,
                                  pcat2 = m.pcat2,
                                  uom = m.uom,
                                  Price = Convert.ToDecimal(p.price),
                                  country = m.country,
                                  region = m.region
                              }).ToList();
                    pd.AddRange(yy);
                    pd = pd.OrderBy(x => long.Parse(x.sku.Substring(1))).ToList();
                }
                GenerateCSVFile.GenerateCSVFiles(pf, "PRODUCT", storeid, folderPath, ftpUserName, ftpPassword);
                GenerateCSVFile.GenerateCSVFiles(pd, "FULLNAME", storeid, folderPath, ftpUserName, ftpPassword);
                Console.WriteLine("Generated POSNation" + storeid + "product csv File........");
                Console.WriteLine("Generated  POSNation" + storeid + "Fullname csv File......");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
    public class clsProductList
    {
        public bool StatusVal { get; set; }
        public int StatusCode { get; set; }
        public string StatusMsg { get; set; }
        public string Price { get; set; }
        public string SessionID { get; set; }

        public string Url { get; set; }
        public class Data
        {
            public string UPC { get; set; }
            public string SKU { get; set; }
            public string ItemName { get; set; }
            public decimal Price { get; set; }
            public decimal Cost { get; set; }
            public decimal SALEPRICE { get; set; }
            public string SizeName { get; set; }
            public string PackName { get; set; }
            public string Vintage { get; set; }
            public string Department { get; set; }
            public decimal PriceA { get; set; }
            public decimal PriceB { get; set; }
            public decimal PriceC { get; set; }
            public decimal total_stock { get; set; }
            public decimal tax { get; set; }
        }

        public class items
        {
            public List<Data> item { get; set; }
        }
    }
    public class PTECHclsProductList
    {
        public bool StatusVal { get; set; }
        public int StatusCode { get; set; }
        public string StatusMsg { get; set; }
        public string Price { get; set; }
        public string SessionID { get; set; }

        public string Url { get; set; }
        public class Data
        {
            public string UPC { get; set; }
            public string SKU { get; set; }
            public string ItemName { get; set; }
            public decimal Price { get; set; }
            public decimal Cost { get; set; }
            public decimal SALEPRICE { get; set; }
            public string SizeName { get; set; }
            public string PackName { get; set; }
            public string Vintage { get; set; }
            public string Department { get; set; }
            public decimal PriceA { get; set; }
            public decimal PriceB { get; set; }
            public decimal PriceC { get; set; }
            public Int32 TotalQty { get; set; }
            public decimal tax { get; set; }
        }
        public class items
        {
            public List<Data> item { get; set; }
        }
    }

    public class datatableModel
    {
        public int StoreID { get; set; }
        public string upc { get; set; }
        public decimal Qty { get; set; }
        public string sku { get; set; }
        public string pack { get; set; }
        public string uom { get; set; }
        public string StoreProductName { get; set; }
        public string StoreDescription { get; set; }
        public decimal Price { get; set; }
        public decimal sprice { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public decimal Tax { get; set; }
        public string altupc1 { get; set; }
        public string altupc2 { get; set; }
        public string altupc3 { get; set; }
        public string altupc4 { get; set; }
        public string altupc5 { get; set; }
        public decimal deposit { get; set; }
        public int discount { get; set; }
    }
    public class ProductsModel
    {
        public int StoreID { get; set; }
        public string upc { get; set; }
        public Int64 Qty { get; set; }
        public string sku { get; set; }
        public string pack { get; set; }
        public string uom { get; set; }
        public string StoreProductName { get; set; }
        public string StoreDescription { get; set; }
        public decimal Price { get; set; }
        public decimal sprice { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public decimal Tax { get; set; }
        public string altupc1 { get; set; }
        public string altupc2 { get; set; }
        public string altupc3 { get; set; }
        public string altupc4 { get; set; }
        public string altupc5 { get; set; }
        public decimal Deposit { get; set; }

    }
    class FullNameProductModel
    {
        public string pname { get; set; }
        public string pdesc { get; set; }
        public string upc { get; set; }
        public string sku { get; set; }
        public decimal Price { get; set; }
        public string uom { get; set; }
        public int pack { get; set; }
        public string pcat { get; set; }
        public string pcat1 { get; set; }
        public string pcat2 { get; set; }
        public string country { get; set; }
        public string region { get; set; }
    }
    public class Modifier
    {
        public string upc { get; set; }
        public int actualqty { get; set; }
        public string type { get; set; }
        public string qty { get; set; }
        public string minqty { get; set; }
        public string maxqty { get; set; }
        public string name { get; set; }
        public string price { get; set; }
        public string cost { get; set; }
        public string lastcost { get; set; }
        public string margin { get; set; }
        public string markup { get; set; }
        public bool viewonprompt { get; set; }
    }
}
