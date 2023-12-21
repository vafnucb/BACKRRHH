using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using Microsoft.Ajax.Utilities;
using UcbBack.Controllers;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using System.Configuration;

namespace UcbBack.Logic.ExcelFiles
{
    public class ContractExcel : ValidateExcelFile
    {

        private static Excelcol[] AltaCols = new[]
        {
            new Excelcol("CUNI", typeof(string)),
            new Excelcol("Documento", typeof(string)),
            //new Excelcol("Expedido", typeof(string)),
            //new Excelcol("Tipo documento de identificacion", typeof(string)),
            new Excelcol("Primer Apellido", typeof(string)),
            new Excelcol("Segundo Apellido", typeof(string)),
            new Excelcol("Nombres", typeof(string)),
            new Excelcol("Apellido casada", typeof(string)),
            //new Excelcol("Genero", typeof(string)),
            //new Excelcol("AFP", typeof(string)),
            //new Excelcol("NUA", typeof(string)),
            //new Excelcol("Fecha Nacimiento", typeof(DateTime)),
            new Excelcol("Dependencia", typeof(string)),
            // new Excelcol("Cargo", typeof(string)), default DTH
            // new Excelcol("Descripcion de Cargo", typeof(string)), default DTH
            // new Excelcol("Dedicacion", typeof(string)), default DTH
            // new Excelcol("Vinculacion", typeof(string)), default DTH
            // new Excelcol("Fecha Inicio", typeof(DateTime)),
            // new Excelcol("Fecha Fin", typeof(DateTime))
        };

        private ValidatePerson validate;
        private int Segment;
        private DateTime startDate;
        private DateTime endDate;
        private ApplicationDbContext _context;

        public ContractExcel(Stream data, ApplicationDbContext context, string fileName, int Segment, DateTime startDate,DateTime endDate,int headerin = 1,
            int sheets = 1, string resultfileName = "AltasTHExcelResult")
            : base(AltaCols, data, fileName, headerin: headerin, resultfileName: resultfileName, sheets: sheets)
        {
            this.Segment = Segment;
            this.startDate = startDate;
            this.endDate = endDate;
            _context = context;
            validate = new ValidatePerson();
            isFormatValid();
        }

        public ContractExcel(string fileName, int headerin = 1, string resultfileName = "AltasTHExcelResult")
            : base(AltaCols, fileName, headerin)
        {
        }

        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();
            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var TempAlta = ToTempAlta(i);
                _context.TempAltas.Add(TempAlta);
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public override bool ValidateFile()
        {
            bool v0 = VerifyPerson(ci:2, CUNI:1, fullname:3, personActive: false);
            //bool v1 = VerifyColumnValueIn(3, new List<string> {"LP", "CB", "SC", "TJ", "OR", "CH", "BN", "PA", "PT"});
            //bool v2 = VerifyColumnValueIn(4, new List<string> {"CI", "CE", "PA"});
            //bool v3 = VerifyColumnValueIn(9, new List<string> {"M", "F"});
            //bool v4 = VerifyColumnValueIn(10, new List<string> {"FUT", "PREV"});
            bool v5 = VerifyColumnValueIn(7,
                _context.Dependencies.Where(x => x.BranchesId == this.Segment && x.Active && x.Academic).Select(m => m.Cod).Distinct().ToList(),
                comment:
                "Esta Dependencia no existe en la Base de Datos Nacional, o no es posible asociar docentes a esta Dependencia.");
            bool v2 = VerifyColumnValueIn(1,
                _context.ContractDetails.Where(x => x.Active).Select(m => m.CUNI).Distinct().ToList(),
                comment:
                "Esta Persona ya tiene un registro activo en la base de datos nacional.", notin:true);
            bool v6 = completeDep();
            return isValid() && v0 && v5 && v6 && v2;
        }
        public bool completeDep()
        {
            bool result = true;
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();
            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                if (wb.Worksheet(1).Cell(i, 7).Value.ToString() == "")
                {
                    result = false;
                    string oldDep = _context.Database.SqlQuery<string>("select \"DependencyCod\" from \"" + CustomSchema.Schema + "\".lastcontracts where cuni = '" + wb.Worksheet(1).Cell(i, 1).Value.ToString() + "'").ToList()[0];
                    wb.Worksheet(1).Cell(i, 7).Value = oldDep;
                }
            }

            return result;
        }

        public TempAlta ToTempAlta(int row, int sheet = 1)
        {

            TempAlta alta = new TempAlta();
            alta.CUNI = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            alta.Document = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            alta.Id = TempAlta.GetNextId(_context);
            //alta.Ext = wb.Worksheet(sheet).Cell(row, 3).Value.ToString().ToUpper();
            //alta.TypeDocument = wb.Worksheet(sheet).Cell(row, 4).Value.ToString().ToUpper();
            alta.FirstSurName = wb.Worksheet(sheet).Cell(row, 3).Value.ToString().ToUpper();
            alta.SecondSurName = wb.Worksheet(sheet).Cell(row, 4).Value.ToString().ToUpper();
            alta.Names = wb.Worksheet(sheet).Cell(row, 5).Value.ToString().ToUpper();
            alta.MariedSurName = wb.Worksheet(sheet).Cell(row, 6).Value.ToString().ToUpper();
            //alta.Gender = wb.Worksheet(sheet).Cell(row, 9).Value.ToString().ToUpper();
            //alta.AFP = wb.Worksheet(sheet).Cell(row, 10).Value.ToString().ToUpper();
            //alta.NUA = wb.Worksheet(sheet).Cell(row, 11).Value.ToString();
            //alta.BirthDate = wb.Worksheet(sheet).Cell(row, 12).GetDateTime();
            alta.Dependencia = wb.Worksheet(sheet).Cell(row, 7).Value.ToString().Replace(".","");
            alta.StartDate = this.startDate;
            alta.EndDate = this.endDate;
            alta.BranchesId = this.Segment;
            alta.State = "UPLOADED";

            return alta;
        }
    };
}