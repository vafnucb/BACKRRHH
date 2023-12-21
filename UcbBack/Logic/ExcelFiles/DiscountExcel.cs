using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Dist;

namespace UcbBack.Logic.ExcelFiles
{
    public class DiscountExcel : ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Socio de Negocio", typeof(string)), 
            new Excelcol("Aclaración del Socio de negocio", typeof(string)),
            new Excelcol("Tipo", typeof(string)),
            new Excelcol("Importe", typeof(string)),
        };
        private ApplicationDbContext _context;
        private string mes, gestion, segmentoOrigen;
        private Dist_File file;
        public DiscountExcel(Stream data, ApplicationDbContext context, string fileName, string mes, string gestion, string segmentoOrigen, Dist_File file,int headerin = 3, int sheets = 1, string resultfileName = "Result")
            : base(cols, data, fileName, headerin, sheets, resultfileName)
        {
            this.segmentoOrigen = segmentoOrigen;
            this.gestion = gestion;
            switch (mes)
            {
                case "13":
                    this.mes = "01";
                    break;
                case "14":
                    this.mes = "02";
                    break;
                case "15":
                    this.mes = "03";
                    break;
                case "16":
                    this.mes = "04";
                    break;
                default:
                    this.mes = mes;
                    break;
            }
            this.file = file;
            _context = context;
            isFormatValid();
        }
        public DiscountExcel(string fileName, int headerin = 1)
            : base(cols, fileName, headerin)
        { }
        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();

            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                _context.DistDiscountses.Add(ToDistDiscounts(i));
            }

            _context.SaveChanges();
        }

        public override bool ValidateFile()
        {
            var connB1 = B1Connection.Instance();
            bool v1 = VerifyColumnValueIn(1, connB1.getBusinessPartners().Cast<string>().ToList(), comment: "Este Codigo de Socio de Negocio no existe en SAP.");
            bool v2 = VerifyColumnValueIn(2, connB1.getBusinessPartners(col: "CardName").Cast<string>().ToList(), comment: "Este nombre de Socio de Negocio no existe en SAP.");
            bool v3 = VerifyColumnValueIn(3, new List<string> { "D_ANTI", "D_REND", "D_OTR", "D_PCOB", "D_RCIVA" }, comment: "Tipo de deducción no valido");
            return isValid() && v1 && v2 && v3;
        }

        public Dist_Discounts ToDistDiscounts(int row, int sheet = 1)
        {
            Dist_Discounts dis = new Dist_Discounts();
            dis.Id = Dist_Discounts.GetNextId(_context);
            dis.BussinesPartner = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            dis.Name = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            dis.Type = wb.Worksheet(sheet).Cell(row, 3).Value.ToString();
            dis.Total = strToDecimal(row, 4);

            dis.mes = this.mes;
            dis.gestion = this.gestion;
            dis.segmentoOrigen = this.segmentoOrigen;

            dis.DistFileId = file.Id;
            return dis;
        }
    }
}