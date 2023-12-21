using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Models;

namespace UcbBack.Logic.ExcelFiles
{
    public class AltasPersonalExcel : ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Carnet Identidad", typeof(string)), 
            new Excelcol("Expedido", typeof(string)), 
            new Excelcol("Tipo documento de identificacion", typeof(string)), 
            new Excelcol("Primer Apellido", typeof(string)),
            new Excelcol("Segundo Apellido", typeof(string)),
            new Excelcol("Nombres", typeof(string)),
            new Excelcol("Apellido Casada", typeof(string)),
            new Excelcol("Fecha Efectiva", typeof(DateTime)),
            new Excelcol("AFP", typeof(string)),
            new Excelcol("NUA", typeof(string)),
            new Excelcol("Fecha nacimiento", typeof(DateTime)),
            new Excelcol("tipo vinculacion", typeof(string)),
            new Excelcol("Dedicacion", typeof(string)),
            new Excelcol("Codigo Dependencia", typeof(int)),
            new Excelcol("Nombre Dependencia", typeof(string)),
            new Excelcol("Cargo / puesto", typeof(string))
        };

        private string segmento;


        private ApplicationDbContext _context;
        public AltasPersonalExcel(Stream data, ApplicationDbContext context, string fileName, string segmentoOrigen, int headerin = 1, int sheets = 1, string resultfileName = "Result")
            : base(cols, data, fileName, headerin, sheets, resultfileName)
        {
            this.segmento = segmentoOrigen;
            _context = context;
            isFormatValid();
        }

        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();

            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                
            }
        }

        public People GetPerson(int row, int sheet = 1)
        {
            People res = null;
            res = _context.Person.FirstOrDefault(x => x.Document == wb.Worksheet(sheet).Cell(row, 1).Value.ToString());
            if (res == null)
            {

            }
            return res;
        }

        public override bool ValidateFile()
        {
            return true;
        }
    }
}