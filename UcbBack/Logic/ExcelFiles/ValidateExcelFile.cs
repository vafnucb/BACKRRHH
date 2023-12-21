using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2013.Word;
using ExcelDataReader;
using Newtonsoft.Json.Linq;
using UcbBack.Logic.B1;
using UcbBack.Models;
using System.Data.Entity;
using System.Diagnostics;
using UcbBack.Controllers;
using UcbBack.Models.Auth;
using UcbBack.Models.Dist;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;


namespace UcbBack.Logic
{
    public struct Excelcol
    {
        public string headers;
        public Type typeofcol;
        public Excelcol(string h, Type t)
        {
            headers = h;
            typeofcol = t;
        }
    }
    public abstract class ValidateExcelFile
    {
        private Excelcol[] columns { get; set; }
        //private DataTable data { get; set; }
        private string fileName { get; set; }
        private string resultfileName { get; set; }
        public XLWorkbook wb { get; private set; }
        public bool valid { get; set; }
        private int sheets { get; set; }
        public int headerin { get; private set; }
        private HanaValidator hanaValidator;
        //Image logo = Image.FromFile(HttpContext.Current.Server.MapPath("~/Images/logo.png"));
        private dynamic errors = new JObject();
        private ValidatePerson personValidator;
        private ApplicationDbContext _context;

        public ValidateExcelFile(Excelcol[] columns, string fileName, int headerin = 1,ApplicationDbContext context=null)
        {
            this.columns = columns;
            this.fileName = fileName;
            this.headerin = headerin;
            _context = context ?? new ApplicationDbContext();
            valid = true;
        }

        public ValidateExcelFile(Excelcol[] columns, Stream data, string fileName, int headerin =1, int sheets = 1, string resultfileName = "Result", ApplicationDbContext context=null)
        {
            _context = context ?? new ApplicationDbContext();
            this.columns = columns;
            this.fileName = fileName;
            this.resultfileName = resultfileName;
            this.sheets = sheets;
            this.headerin = headerin;
            this.wb = setExcelFile(data);
            hanaValidator = new HanaValidator();
            personValidator = new ValidatePerson();
            valid = true;
        }

        public abstract void toDataBase();
        public abstract bool ValidateFile();

        public bool isValid()
        {
            return valid;
        }

        public HttpResponseMessage getTemplate()
        {
            var template = new XLWorkbook();
            IXLWorksheet ws =template.AddWorksheet(fileName.Replace(".xlsx",""));
            var tittle = ws.Range(1, 1, 2, columns.Length);
            tittle.Cell(1, 1).Value = fileName.Replace(".xlsx", "").ToUpper();//"Base de Datos Nacional de Capital Humano";
            tittle.Cell(1, 1).Style.Font.Bold = true;
            tittle.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
            tittle.Cell(1, 1).Style.Font.FontName = "Bahnschrift SemiLight";
            tittle.Cell(1, 1).Style.Font.FontSize = 20;
            tittle.Cell(1, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            tittle.Merge();
            for (int i = 0; i < columns.Length; i++)
            {
                ws.Column(i + 1).Width=13;
                ws.Cell(headerin, i + 1).Value = columns[i].headers;
                /*if(columns[i].typeofcol==typeof(double))
                    for (int j = headerin + 1; j < 1000; j++)
                    {
                        ws.Cell(j, i + 1).Style.NumberFormat.Format = "#,##0.00";
                        var validation = ws.Cell(j, i + 1).DataValidation;
                        validation.Decimal.Between(0, 9999999);
                        validation.InputTitle = "Columna Numerica";
                        validation.InputMessage = "Por favor ingresar solo numeros.";
                        validation.ErrorStyle = XLErrorStyle.Warning;
                        validation.ErrorTitle = "Error de tipo de valor";
                        validation.ErrorMessage = "Esta celda debe ser tipo numerica.";
                    }*/

                ws.Cell(headerin, i + 1).Style.Alignment.WrapText = true;
                ws.Cell(headerin, i + 1).Style.Font.Bold = true;
                ws.Cell(headerin, i + 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
            }

            valid = true;
            return toResponse(template);
        }

        public bool isFormatValid()
        {
            bool res = true;
            if (sheets != wb.Worksheets.Count)
            {
                addError("Cantidad de Hojas", "Se envio un archivo con " + wb.Worksheets.Count + " hojas, se esperaba tener " + sheets + ", solo se revisó la" + (sheets > 1 ? "s" : "") + " primera" + (sheets > 1 ? "s" : "") + " hoja" + (sheets > 1 ? "s" : "") );
                res = false;
            }

            for(int l = 1 ;l<=sheets;l++)
            {
                var sheet = wb.Worksheet(l);
                IXLRange UsedRange = sheet.RangeUsed();
                /*if (UsedRange==null)
                {
                    addError("Archivo Sin Datos", "No se encontró datos en el archivo subido.");
                    return false;
                }*/
                if (UsedRange.ColumnCount() != columns.Length)
                {
                    addError("Cantidad de Columnas", "Se esperaba tener " + columns.Length + "columnas en la hoja: " + sheet.Name + " se encontró " + UsedRange.ColumnCount());
                    res = false;
                }

                if (UsedRange.LastRow().RowNumber() <= headerin)
                {
                    addError("Archivo Sin Datos", "No se encontró datos en el archivo subido.");
                    res = false;
                }
                for (int i = 1; i <= columns.Length; i++)
                {
                    var comp = String.Compare(
                        Regex.Replace(sheet.Cell(headerin, i).Value.ToString().Trim().ToUpper().Replace("_"," "), @"\t|\n|\r", " "),
                        columns[i - 1].headers.Trim().ToUpper(), CultureInfo.CurrentCulture, CompareOptions.IgnoreNonSpace);
                    if (comp!=0)
                    {
                        res = false;
                        addError("Nombre de columna", "La columna " + i + "deberia llamarse: " + columns[i - 1].headers,false);
                        paintXY(i, headerin, XLColor.Red, "Esta Columna deberia llamarse: " + columns[i - 1].headers);
                    }
                    bool tipocol = true;
                    if (columns[i - 1].typeofcol != typeof(string))    
                    for (int j = headerin + 1; j < UsedRange.LastRow().RowNumber(); j++)
                    {
                        
                        if (sheet.Cell(j, i).Value.GetType() != columns[i - 1].typeofcol)
                        {
                            res = false;
                            var xx = sheet.Cell(j, i).Value;
                            paintXY(i, j, XLColor.Red, "Esta Celda deberia ser tipo: " + columns[i - 1].typeofcol.Name);
                            if (tipocol)
                            {
                                addError("Tipo de valor de columna", "La columna " + i + " deberia ser tipo: " + columns[i - 1].typeofcol.Name, false);
                                tipocol = false;
                            }
                        }
                       
                    }
                }
            }

            valid = valid && res;
            return res;
        }

        public bool VerifyColumnValueIn(
            int index,
            List<string> list,
            bool paintcol=true,
            int sheet=1,
            string comment = "Este Valor no es permitido en esta columna.",
            bool jaro=false,
            string colToCompare=null, 
            string table=null, 
            string colId=null,
            bool notin=false)
        {
            try
            {
                bool res = true;
                IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
                var l = UsedRange.LastRow().RowNumber();
                //se toma el número de filas que se deben revisar, sin contar la cabecera. Por eso empieza en 2
                for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
                {
                    var value = cleanText(wb.Worksheet(sheet).Cell(i, index).Value.ToString());
                    if (list.Exists(x => string.Equals(cleanText(x), value, StringComparison.OrdinalIgnoreCase)) ==
                        notin)
                    {
                        res = false;
                        if (paintcol)
                        {
                            string aux = "";
                            if (jaro)
                            {
                                var similarities = hanaValidator.Similarities(
                                    wb.Worksheet(sheet).Cell(i, index).Value.ToString(), colToCompare, table, colId,
                                    0.9f);
                                aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                            }

                            paintXY(index, i, XLColor.Red, comment + aux);
                        }

                    }
                }

                valid = valid && res;
                if (!res)
                    addError("Valor no valido", "Valor o valores no validos en la columna: " + index, false);
                return res;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }
        public bool VerifyColumnValueInWithSpace(
            int index,
            List<string> list,
            bool paintcol = true,
            int sheet = 1,
            string comment = "Este Valor no es permitido en esta columna.",
            bool jaro = false,
            string colToCompare = null,
            string table = null,
            string colId = null,
            bool notin = false)
        {
            try
            {
                bool res = true;
                IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
                var l = UsedRange.LastRow().RowNumber();
                //se toma el número de filas que se deben revisar, sin contar la cabecera. Por eso empieza en 2
                for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
                {
                    var value = wb.Worksheet(sheet).Cell(i, index).Value.ToString();
                    if (list.Exists(x => string.Equals(cleanText(x), value, StringComparison.OrdinalIgnoreCase)) ==
                        notin)
                    {
                        res = false;
                        if (paintcol)
                        {
                            string aux = "";
                            if (jaro)
                            {
                                var similarities = hanaValidator.Similarities(
                                    wb.Worksheet(sheet).Cell(i, index).Value.ToString(), colToCompare, table, colId,
                                    0.9f);
                                aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                            }
                            bool fHasSpace = value.Contains(" ");
                            if (fHasSpace)
                            {
                                aux = "(Probablemente la celda contenga un espacio)";
                            }
                            paintXY(index, i, XLColor.Red, comment + aux);
                        }

                    }
                }

                valid = valid && res;
                if (!res)
                    addError("Valor no valido", "Valor o valores no validos en la columna: " + index, false);
                return res;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }
        public string cleanText(string a)
        {
            var res = a == null
                ? null
                : a.Replace("Á", "A").Replace("É", "E")
                    .Replace("Í", "I").Replace("Ó", "O")
                    .Replace("Ú", "U").Replace("  ", " ")
                    .Replace("'","").Replace("´","").Replace("`","");
            return res==null?null:res.EndsWith(" ")?res.Substring(0, res.Length - 1):res;
        }

        public bool VerifyPerson(int ci = -1, int CUNI = -1, int fullname = -1, int sheet = 1, int dependency = -1, bool paintdep=false, bool paintcolci = true, bool paintcolcuni = true, bool paintcolnombre = true, bool jaro = true, string comment = "No se encontró este valor en la Base de Datos Nacional.", bool personActive = true, string date = null, string format = "yyyy-MM-dd", int branchesId =-1,int tipo =-1)
        {
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var c = new ApplicationDbContext();
            var ppllist = c.Person.ToList();
            int n = UsedRange.LastRow().RowNumber();
            for (int i = headerin + 1; i <= n; i++)
            {
                string strname = null;
                string strfsn = null;
                string strssn = null;
                string strmsn = null;
                try
                {
                    string strci = ci != -1 ? wb.Worksheet(sheet).Cell(i, ci).Value.ToString() : null;
                    string strcuni = CUNI != -1 ? wb.Worksheet(sheet).Cell(i, CUNI).Value.ToString() : null;
                    string strdep = dependency != -1 ? wb.Worksheet(sheet).Cell(i, dependency).Value.ToString() : null;
                    string strtipo = tipo != -1 ? wb.Worksheet(sheet).Cell(i, tipo).Value.ToString() : null;

                    if (fullname != -1)
                    {
                        strfsn = cleanText(wb.Worksheet(sheet).Cell(i, fullname).Value.ToString() == "" ? null : wb.Worksheet(sheet).Cell(i, fullname).Value.ToString().ToUpper());

                        strssn = cleanText(wb.Worksheet(sheet).Cell(i, fullname + 1).Value.ToString() == "" ? null : wb.Worksheet(sheet).Cell(i, fullname + 1).Value.ToString().ToUpper());

                        strname = cleanText(wb.Worksheet(sheet).Cell(i, fullname + 2).Value.ToString() == "" ? null : wb.Worksheet(sheet).Cell(i, fullname + 2).Value.ToString().ToUpper());

                        strmsn = cleanText(wb.Worksheet(sheet).Cell(i, fullname + 3).Value.ToString() == "" ? null : wb.Worksheet(sheet).Cell(i, fullname + 3).Value.ToString().ToUpper());

                    }

                    var p = ppllist.FirstOrDefault(x => x.CUNI == strcuni);
                    if (paintdep && p!=null)
                    {
                        if (tipo != -1 && strtipo != "TH")
                        {
                            if (!personValidator.IspersonDependency(p, strdep, date, format))
                            {
                                res = false;
                                paintXY(dependency, i, XLColor.Red, "Esta Persona NO se encuentra en esta Dependencia, según la base de datos nacional\n");
                            }
                        }

                        if (strtipo == "TH")
                        {
                            if (!personValidator.IspersonBranch(p, strdep, branchesId, date, format))
                            {
                                res = false;
                                paintXY(ci, i, XLColor.Red, "Esta Persona NO se encuentra en esta Regional, según la base de datos nacional\n");
                            }
                        }
                        
                    }  
                    if (personActive && p!=null && !personValidator.IsActive(p, date, format))
                    {
                        res = false;
                        if (fullname != -1)
                        {
                            paintXY(fullname, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");
                            paintXY(fullname + 1, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");
                            paintXY(fullname + 2, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");
                            paintXY(fullname + 3, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");

                        }
                        if (ci != -1)
                            paintXY(ci, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");
                        if (CUNI != -1)
                            paintXY(CUNI, i, XLColor.Red, "Esta Persona NO se encuentra Activa\n");
                    }

                    if (!ppllist.Any(x => x.Document == strci
                                        && x.CUNI == strcuni
                                        && cleanText(x.FirstSurName) == strfsn
                                        && (x.UseSecondSurName==0 || cleanText(x.SecondSurName) == strssn)
                                        && cleanText(x.Names) == strname
                                        && (x.UseMariedSurName==0 || cleanText(x.MariedSurName) == strmsn)))
                    {
                        res = false;                       
                        if (strci != null && ppllist.Any(x => x.Document == strci.ToString()))
                        {
                            if (!ppllist.Any(x => x.Document == strci
                                                  && cleanText(x.FirstSurName) == strfsn
                                                  && (x.UseSecondSurName==0 || cleanText(x.SecondSurName) == strssn)
                                                  && cleanText(x.Names) == strname
                                                  && (x.UseMariedSurName==0 || cleanText(x.MariedSurName) == strmsn)))
                            {
                                if (paintcolnombre)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => x.Document == strci.ToString()).Select(y => new { y.UseMariedSurName,y.UseSecondSurName,y.FirstSurName, y.SecondSurName, y.Names, y.MariedSurName }).FirstOrDefault();
                                    aux = similarities!=null && strfsn != similarities.FirstSurName ? "\nNo será: '" + similarities.FirstSurName + "'?" : "";
                                    paintXY(fullname, i, XLColor.Red,  aux);

                                    if (similarities != null && similarities.UseSecondSurName==1)
                                    {
                                        aux = similarities != null && strssn != similarities.SecondSurName ? "\nNo será: '" + similarities.SecondSurName + "'?" : "";
                                        paintXY(fullname + 1, i, XLColor.Red, aux);
                                    }
                                    
                                    aux = similarities != null && strname != similarities.Names ? "\nNo será: '" + similarities.Names + "'?" : "";
                                    paintXY(fullname + 2, i, XLColor.Red, aux);

                                    if (similarities != null && similarities.UseMariedSurName==1)
                                    {
                                        aux = similarities != null && strmsn != similarities.MariedSurName ? "\nNo será: '" + similarities.MariedSurName + "'?" : "";
                                        paintXY(fullname + 3, i, XLColor.Red, aux);
                                    }
                                }
                            }
                            if (strcuni != null && !ppllist.Any(x => x.Document == wb.Worksheet(sheet).Cell(i, ci).Value.ToString()
                                               && x.CUNI == wb.Worksheet(sheet).Cell(i, CUNI).Value.ToString()))
                            {
                                if (paintcolcuni)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => x.Document == strci).Select(y => y.CUNI).ToList();
                                    aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                                    //wb.Worksheet(sheet).Cell(i, CUNI).Value = similarities;
                                    paintXY(CUNI, i, XLColor.Red, comment + aux);
                                }
                            }
                        }
                        else if (strcuni != null && ppllist.Any(x => x.CUNI == strcuni.ToString()))
                        {
                            if (strname != null && !ppllist.Any(x => x.CUNI == strcuni
                                                                     && x.FirstSurName == strfsn
                                                                     && x.SecondSurName == strssn
                                                                     && x.Names == strname
                                                                     && x.MariedSurName == strmsn))
                            {
                                if (paintcolnombre)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => x.Document == strci).Select(y => new { y.FirstSurName, y.SecondSurName, y.Names, y.MariedSurName }).ToList();
                                    aux = similarities.Any() && strfsn != similarities[0].FirstSurName ? "\nNo será: '" + similarities[0].FirstSurName + "'?" : "";
                                    paintXY(fullname, i, XLColor.Red,  aux);

                                    aux = similarities.Any() && strssn != similarities[0].SecondSurName ? "\nNo será: '" + similarities[0].SecondSurName + "'?" : "";
                                    paintXY(fullname + 1, i, XLColor.Red,  aux);

                                    aux = similarities.Any() && strname != similarities[0].Names ? "\nNo será: '" + similarities[0].Names + "'?" : "";
                                    paintXY(fullname + 2, i, XLColor.Red,  aux);

                                    aux = similarities.Any() && strmsn != similarities[0].MariedSurName ? "\nNo será: '" + similarities[0].MariedSurName + "'?" : "";
                                    paintXY(fullname + 3, i, XLColor.Red,  aux);
                                }
                            }
                            if (strci != null && !ppllist.Any(x => x.Document == strci
                                                  && x.CUNI == strcuni))
                            {
                                if (paintcolci)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => x.CUNI == strcuni).Select(y => y.Document).ToList();
                                    aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                                    paintXY(ci, i, XLColor.Red, comment + aux);
                                }
                            }
                        }
                        else if (strname != null && ppllist.Any(x => cleanText(x.FirstSurName) == strfsn
                                                                 && cleanText(x.SecondSurName) == strssn
                                                                 && cleanText(x.Names) == strname))
                        {
                            if (strcuni != null && !ppllist.Any(x => x.CUNI == strcuni
                                                  && cleanText(x.SecondSurName) == strssn
                                                  && cleanText(x.Names) == strname
                                                  && cleanText(x.MariedSurName) == strmsn))
                            {
                                if (paintcolcuni)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => x.FirstSurName == strfsn
                                                                           && x.SecondSurName == strssn
                                                                           && x.Names == strname
                                                                           /*&& x.MariedSurName == strmsn*/).Select(y => y.CUNI).ToList();
                                    aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                                    paintXY(CUNI, i, XLColor.Red, comment + aux);
                                }
                            }
                            if (strci != null && !ppllist.Any(x => x.Document == strci
                                                  && (x.FirstSurName + " " + x.SecondSurName + " " + x.Names) ==
                                                  strname))
                            {
                                if (paintcolci)
                                {
                                    res = false;
                                    string aux = "";
                                    var similarities = ppllist.Where(x => cleanText(x.FirstSurName) == strfsn
                                                                          && cleanText(x.SecondSurName) == strssn
                                                                          && cleanText(x.Names) == strname
                                                                          && cleanText(x.MariedSurName) == strmsn).Select(y => y.Document).ToList();

                                    aux = similarities.Any() ? "\nNo será: '" + similarities[0].ToString() + "'?" : "";
                                    paintXY(ci, i, XLColor.Red, comment + aux);
                                }
                            }
                        }
                        else
                        {
                            string aux = "";
                            if (strname != null && jaro)
                            {
                                res = false;
                                var similarities = hanaValidator.Similarities(strfsn + " " + strssn + " " + strname, "concat(\"FirstSurName\"," + "concat('' ''," + "concat(\"SecondSurName\"," + "concat('' '',\"Names\")" + ")" + ")" + ")", "People", "\"CUNI\"", 0.9f);
                                if (similarities.Count > 0)
                                {
                                    string si = similarities[0];
                                    var person = _context.Person.FirstOrDefault(pe => pe.CUNI == si);
                                    

                                    aux = strfsn != person.FirstSurName ? "\nNo será: '" + person.FirstSurName + "'?" : "";
                                    paintXY(fullname, i, XLColor.Red,  aux);

                                    aux = strssn != person.SecondSurName ? "\nNo será: '" + person.SecondSurName + "'?" : "";
                                    paintXY(fullname + 1, i, XLColor.Red, aux);

                                    aux = strname != person.Names ? "\nNo será: '" + person.Names + "'?" : "";
                                    paintXY(fullname + 2, i, XLColor.Red,  aux);

                                    aux = strmsn != person.MariedSurName ? "\nNo será: '" + person.MariedSurName + "'?" : "";
                                    paintXY(fullname + 3, i, XLColor.Red,  aux);

                                    aux = strci != person.Document ? "\nNo será: '" + person.Document + "'?" : "";
                                    paintXY(ci, i, XLColor.Red,  aux);

                                    aux = strcuni != person.CUNI ? "\nNo será: '" + person.CUNI + "'?" : "";
                                    paintXY(CUNI, i, XLColor.Red, aux);
                                }
                                else
                                {
                                    res = false;
                                    aux = "Esta persona no existe en la Base de Datos Nacional";
                                    paintXY(fullname, i, XLColor.Red, aux);
                                    paintXY(fullname, i+1, XLColor.Red, aux);
                                    paintXY(fullname, i+2, XLColor.Red, aux);
                                    paintXY(fullname, i+3, XLColor.Red, aux);
                                    paintXY(ci, i, XLColor.Red, aux);
                                    paintXY(CUNI, i, XLColor.Red, aux);
                                }
                                                                
                            }
                        }


                    }
                }
                catch (Exception e)
                {
                    paintXY(CUNI, i, XLColor.Red, "Existen Enlaces a otros archivos");
                    Console.WriteLine(e);
                    addError("Existen Enlaces a otros archivos", "Existen celdas con referencias a otros archivos.");
                    valid = false;
                    return false;
                    //throw;
                }
                
            }
            if(!res)
                addError("Datos Personas","Algunos datos de personas no coinciden o no existen en la Base de datos Nacional.");
            valid = valid && res;
            return res;

        }

        public bool VerifyParalel(int cod,int periodo, int sigla, int paralelo, int dependency, int branch, int sheet =1)
        {
            var B1conn = B1Connection.Instance();
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            int branchId = Convert.ToInt16(branch);
            var branchName = _context.Branch.FirstOrDefault(x => x.Id == branchId).Abr;
            List<dynamic> list = B1conn.getParalels();
            var filteredList = list.Where(x => x.segmento == branchName);

            
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var strcod = cod != -1 ? wb.Worksheet(sheet).Cell(i, cod).Value.ToString() : null;
                var strperiodo = periodo != -1 ? wb.Worksheet(sheet).Cell(i, periodo).Value.ToString() : null;
                var strsigla = sigla != -1 ? wb.Worksheet(sheet).Cell(i, sigla).Value.ToString() : null;
                var strparalelo = paralelo != -1 ? wb.Worksheet(sheet).Cell(i, paralelo).Value.ToString() : null;
                //para hacer las validaciones de UO
                var strdependency = dependency != -1 ? wb.Worksheet(sheet).Cell(i, dependency).Value.ToString() : null;
                var dep = _context.Dependencies.Where(x => x.BranchesId == branch).Include(x => x.OrganizationalUnit)
                    .FirstOrDefault(x => x.Cod == strdependency);
                if (dep == null)
                {
                    paintXY(dependency, i, XLColor.Red, "Esta dependencia no existe en la Base de Datos Nacional.");
                    res = false;
                }
                else {
                    //Solo se valida si existe la dep
                    //Si no existe un match del código del paralelo en SAP, el periodo, la sigla y el paralelo del excel con Datos Maestros, especificamos el error...
                    //A´demás de lo validado en el comentario previo, se revisa si hay match con la UO correcta, ya sea de la col OU o de la tabla auxiliar. Debajo se valida la OU
                    if (!filteredList.Any(x => (x.cod == strcod && x.periodo == strperiodo && x.sigla == strsigla && x.paralelo == strparalelo) && (x.OU == dep.OrganizationalUnit.Cod || x.auxiliar == dep.OrganizationalUnit.Cod)))
                    {
                        res = false;
                        if (filteredList.Any(x => x.cod == strcod))//si hay match con el código del paralelo, especificamos cual fue el elemento que no hizo match
                        {
                            var row = filteredList.FirstOrDefault(x => x.cod == strcod);
                            if (row.sigla != strsigla)
                            {
                                paintXY(sigla, i, XLColor.Red, "Esta Sigla no es correcta.");
                            }
                            if (row.periodo != strperiodo)
                            {
                                paintXY(periodo, i, XLColor.Red, "Este Periodo no es correcto.");
                            }
                            if (row.paralelo != strparalelo)
                            {
                                paintXY(paralelo, i, XLColor.Red, "Este Paralelo no es correcto.");
                            }
                            // Verificar UO: si este paralelo tiene una UO auxiliar, esa es la que manda, sino validar con la columna OU
                            if (row.auxiliar != "")
                            {
                                if (dep.OrganizationalUnit.Cod.ToString() != row.auxiliar.ToString())
                                {
                                    string UO = row.auxiliar.ToString();
                                    string UOName = _context.OrganizationalUnits.FirstOrDefault(x => x.Cod == UO).Name;
                                    paintXY(dependency, i, XLColor.Red, "Este paralelo debería tener una dependencia asociada a la UO: " +UOName);
                                }
                            }
                            else
                            {
                                if (dep.OrganizationalUnit.Cod.ToString() != row.OU.ToString())
                                {
                                    string UO = row.OU.ToString();
                                    string UOName = _context.OrganizationalUnits.FirstOrDefault(x => x.Cod == UO).Name;
                                    paintXY(dependency, i, XLColor.Red, "Este paralelo debería tener una dependencia asociada a la UO: " + UOName);
                                }
                            }
                        }
                        else
                        {
                            paintXY(cod, i, XLColor.Red, "Este código de paralelo no existe en SAP, al menos para esta regional");
                        }
                    }
                }
            }

            valid = valid && res;
            if (!res)
                addError("Datos Paralelos", "Algunos datos de paralelos no coinciden o no existen en SAP B1.");
            return res;
        }


        public Decimal strToDecimal(int row, int col, int sheet=1)
        {
            return wb.Worksheet(sheet).Cell(row, col).Value.ToString() == "" ? 0.0m : Decimal.Parse(wb.Worksheet(sheet).Cell(row, col).Value.ToString());
        }
        public Double strToDouble(int row, int col, int sheet=1)
        {
           return wb.Worksheet(sheet).Cell(row, col).Value.ToString() == "" ? 0.0 : Double.Parse(wb.Worksheet(sheet).Cell(row, col).Value.ToString());            
        }

        public void paintXY(int x, int y,XLColor color,string comment = null)
        {
            wb.Worksheet(1).Cell(y, x).Style.Fill.BackgroundColor = color;
            if (!string.IsNullOrEmpty(comment))
            {
                wb.Worksheet(1).Cell(y, x).Comment.Style.Alignment.SetAutomaticSize();
                wb.Worksheet(1).Cell(y, x).Comment.AddText(comment);
            }
        }

        public HttpResponseMessage toResponse(XLWorkbook w = null)
        {
            w = w ?? wb;
            HttpResponseMessage response = new HttpResponseMessage();
            var ms = new MemoryStream();
            if (w != null)
            {
                resultfileName = resultfileName == null ? fileName: resultfileName;
                resultfileName = resultfileName.Replace(".xlsx", "");
                w.Author = "PersoNAS UCB";
                w.SaveAs(ms);
                response.StatusCode = valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = resultfileName;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                response.Headers.Add("UploadErrors",errors.ToString().Replace("\r\n",""));
                ms.Seek(0, SeekOrigin.Begin); 
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", errors.ToString());
                response.Content = new StringContent("Formato del archivo no valido.");
            }
            return response;
        }

        public bool VerifyLength(int col, int length, int sheet = 1)
        {
            bool res = true;
            string commnet = "Este Campo es demasiado Grande. El limite es: " + length + " caracteres.";

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var a = wb.Worksheet(sheet).Cell(i, col).Value.ToString();
                if (a.Length > length)
                {
                    res = false;
                    paintXY(col, i, XLColor.Red, commnet);
                }
            }

            valid = valid && res;
            if (!res)
                addError("Valor no valido", "Valor o valores muy largos en la columna: " + col, false);
            return res;
        }

        public bool VerifyNotEmpty(int col, int sheet = 1)
        {
            bool res = true;
            string commnet = "Este Campo no puede ser vacio";

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var a = wb.Worksheet(sheet).Cell(i, col).Value.ToString();
                if (a.Replace(" ","").Replace("0","").Replace(".","") == "")
                {
                    res = false;
                    paintXY(col, i, XLColor.Red, commnet);
                }
            }

            valid = valid && res;
            if (!res)
                addError("Valor no valido", "La columna: " + col + " no puede tener valores vacios",false);
            return res;
        }

        public bool VerifyBP(int iCardCode,int iCardName,int BranchesId, CustomUser user, int sheet = 1)
        {
            bool res = true;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var CardCode = wb.Worksheet(sheet).Cell(i, iCardCode).Value.ToString();
                var CardName = wb.Worksheet(sheet).Cell(i, iCardName).Value.ToString();
                //var BP = _context.Civils.FirstOrDefault(x => x.SAPId == CardCode);
                //todo comparar con el nombre que esta en SAP ya no usar el nombre que está en la tabla CIVIL solo utilizar el código
                //todo verificar la validacion del socio de negocio con la regional en sap
                //var BPog = 
                Civil BP = _context.Database.SqlQuery<Civil>("select c.\"Id\", ocrd.\"CardName\" \"FullName\", c.\"SAPId\", c.\"NIT\", c.\"Document\", c.\"BranchesId\", c.\"CreatedBy\"\r\nfrom "+ ConfigurationManager.AppSettings["B1CompanyDB"] +".ocrd\r\ninner join " + CustomSchema.Schema + ".\"Civil\" c on c.\"SAPId\" = ocrd.\"CardCode\"\r\nwhere c.\"SAPId\" = '" + CardCode + "'").FirstOrDefault();

                if (BP == null)
                {
                    res = false;
                    paintXY(iCardCode, i, XLColor.Red, "Este Codigo de Socio de Negocio no es valido como Civil, ¿No olvidó registrarlo?");
                    paintXY(iCardName, i, XLColor.Red, "Este Codigo de Socio de Negocio no es valido como Civil, ¿No olvidó registrarlo?");
                }
                else
                {
                    var BPInSAP = Civil.findBPInSAP(BP.SAPId, user, _context).FirstOrDefault(x => x.BranchesId == BranchesId);
                    var testVar = "test";
                    if (BPInSAP == null)
                    {
                        res = false;
                        paintXY(iCardCode, i, XLColor.Red, "Este Codigo de Socio de Negocio no es valido para esta Regional.");
                        paintXY(iCardName, i, XLColor.Red, "Este Codigo de Socio de Negocio no es valido para esta Regional.");
                    }
                    else if (cleanText(BP.FullName) != cleanText(CardName))
                    {
                        res = false;
                        paintXY(iCardName, i, XLColor.Red, "El nombre de este Socio de Negocio es incorrecto, no será: " + BP.FullName);
                    }
                }
            }

            valid = valid && res;
            if (!res)
                addError("Valor no valido", "Valor o valores no validos en la Columna: " + iCardCode, false);
            return res;
        }

        public bool VerifyCareer(int cod, int branch,  int dependency,int sheet = 1)
        {
            var B1conn = B1Connection.Instance();
            int branchId = Convert.ToInt16(branch);
            var branchName = _context.Branch.FirstOrDefault(x => x.Id == branchId).Abr;
            List<dynamic> list = B1conn.getCareers();
            var filteredList = list.Where(x => x.segmento == branchName);
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var strcod = cod != -1 ? wb.Worksheet(sheet).Cell(i, cod).Value.ToString() : null;
                var strdependency = dependency != -1 ? wb.Worksheet(sheet).Cell(i, dependency).Value.ToString() : null;
                var dep = _context.Dependencies.Where(x => x.BranchesId == branch).Include(x => x.OrganizationalUnit).FirstOrDefault(x => x.Cod == strdependency);
                if (dep == null)
                {
                    //Si no existe la dependencia entonces no podemos validar
                    paintXY(dependency, i, XLColor.Red, "Esta dependencia no existe en la base de Datos Nacional");
                    res = false;
                }
                else {
                    //Si no existe un match del código de la carrera
                    if (!filteredList.Any(x => x.cod == strcod && x.OU == dep.OrganizationalUnit.Cod))
                    {
                        res = false;
                        if (filteredList.Any(x => x.cod == strcod))
                        {
                            var row = filteredList.FirstOrDefault(x => x.cod == strcod);
                            // Verifica UO
                            if (dep.OrganizationalUnit.Cod.ToString() != row.OU.ToString())
                            {
                                string UO = row.OU.ToString();
                                string UOName = _context.OrganizationalUnits.FirstOrDefault(x => x.Cod == UO).Name;
                                paintXY(dependency, i, XLColor.Red, "Esta carrera debería tener una dependencia asociada a la UO: " + UOName);
                            }
                        }
                        else
                        {
                            paintXY(cod, i, XLColor.Red, "Esta carrera no existe, al menos para esta regional");
                        }
                    }
                }
            }

            valid = valid && res;
            if (!res)
                addError("Datos Carrera", "Algunos datos de la carrera no coinciden o no existen en SAP B1.");
            return res;
        }

        public XLWorkbook setExcelFile(Stream stream)
        {
            wb = new XLWorkbook(stream);
            return wb;
        }

        public void addError(string error_name,string err,bool replace = true)
        {
            valid = false;
            error_name = HttpUtility.HtmlEncode(error_name);
            err = HttpUtility.HtmlEncode(err);
            errors[error_name] = replace ? err : errors[error_name] == null ? errors[error_name] = err : errors[error_name]+","+err;
        }
    }
}