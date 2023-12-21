using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Models;

namespace UcbBack.Logic.ExcelFiles
{
    public class ValidateContractsFile:ValidateExcelFile
    {
        private static Excelcol[] cols = new[] {new Excelcol("id", typeof(double)), new Excelcol("nombre", typeof(string))};
        private ApplicationDbContext _context;
        public ValidateContractsFile(Stream d, string fn, ApplicationDbContext c, string rfn ="ContractResult")
            : base(cols, d, fn,resultfileName:rfn)
        {
            _context = c;
            isFormatValid();

        }

        public bool validatefile()
        {
            if (!isFormatValid()) return isFormatValid();
            return  VerifyColumnValueIn(0, _context.Person.Select(m => m.CUNI).Distinct().ToList());
        }


        public override void toDataBase()
        {
            throw new NotImplementedException();
        }

        public override bool ValidateFile()
        {
            throw new NotImplementedException();
        }
    }
}