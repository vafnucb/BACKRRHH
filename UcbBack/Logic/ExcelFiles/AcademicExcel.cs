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
    public class AcademicExcel : ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Carnet Identidad", typeof(string)), 
            new Excelcol("Primer Apellido", typeof(string)),
            new Excelcol("Segundo Apellido", typeof(string)),
            new Excelcol("Nombres", typeof(string)),
            new Excelcol("Apellido Casada", typeof(string)),
            new Excelcol("Tipo empleado", typeof(string)),
            new Excelcol("Periodo académico", typeof(string)),
            new Excelcol("Sigla Asignatura", typeof(string)),
            new Excelcol("Paralelo", typeof(string)),
            new Excelcol("Horas Académicas por semana", typeof(double)),
            new Excelcol("Horas Académicas por mes", typeof(double)),
            new Excelcol("Identificador de Pago", typeof(string)),
            new Excelcol("Categoría de docente", typeof(string)),
            new Excelcol("Costo hora", typeof(double)),
            new Excelcol("Costo mes", typeof(double)),
            new Excelcol("CUNI", typeof(string)),
            new Excelcol("Identificador de dependencia", typeof(string)),
            new Excelcol("PEI-PO", typeof(string)),
            new Excelcol("Codigo Paralelo SAP", typeof(string)),
        };
        private ApplicationDbContext _context;
        private string mes, gestion, segmentoOrigen;
        private Dist_File file;

        public AcademicExcel(Stream data, ApplicationDbContext context, string fileName,string mes, string gestion, string segmentoOrigen,Dist_File file,int headerin = 3, int sheets = 1, string resultfileName = "Result")
            : base(cols, data, fileName, headerin, sheets, resultfileName)
        {
            this.segmentoOrigen = segmentoOrigen;
            this.gestion = gestion;
            //change month for validation purposes
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
        public AcademicExcel(string fileName, int headerin = 1)
            : base(cols, fileName, headerin)
        { }

        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();

            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                _context.DistAcademics.Add(ToDistAcademic(i));
            }

            _context.SaveChanges();
        }

        public override bool ValidateFile()
        {
            var connB1 = B1Connection.Instance();
            if (!connB1.connectedtoHana)
            {
                addError("Error en SAP", "No se puedo conectar con SAP B1, es posible que algunas validaciones cruzadas con SAP no sean ejecutadas");
            }
            int brId = Int32.Parse(this.segmentoOrigen);
            bool v1 = VerifyPerson(ci:1, CUNI:16, fullname:2,personActive:false);
            bool v2 = VerifyColumnValueIn(6, _context.TipoEmpleadoDists.Select(x => x.Name).ToList(), comment: "Este Tipo empleado no es valido.");
            bool v3 = VerifyParalel(cod: 19, periodo: 7, sigla: 8, paralelo: 9, dependency: 17, branch: brId);
            bool v4 = VerifyColumnValueIn(12, new List<string> { "PA", "PI", "TH" });
            var pei = connB1.getCostCenter(B1Connection.Dimension.PEI, mes: this.mes, gestion: this.gestion).Cast<string>().ToList();
            pei.Add("0");
            bool v5 = VerifyColumnValueIn(18, pei, comment: "Este PEI no existe en SAP.");
            bool v6 = VerifyColumnValueIn(11, new List<string> { "0" }, comment: "Este valor no puede ser 0", notin: true);
            bool v7 = VerifyColumnValueIn(15, new List<string> { "0" }, comment: "Este valor no puede ser 0", notin: true);
            bool v0 = isValid();
            var xx = valid;

            //return v0 && v1 && v2 && v4 && v7 && v8 && v6 && v5;//v3
            return v0 && v1 && v2 && v3 && v4 && v5 && v6 && v7;
        }

        public Dist_Academic ToDistAcademic(int row,int sheet = 1)
        {
            Dist_Academic acad = new Dist_Academic();
            acad.Id = Dist_Academic.GetNextId(_context);
            acad.Document = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            acad.Names = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            acad.FirstSurName = wb.Worksheet(sheet).Cell(row, 3).Value.ToString();
            acad.SecondSurName = wb.Worksheet(sheet).Cell(row, 4).Value.ToString();
            acad.MariedSurName = wb.Worksheet(sheet).Cell(row, 5).Value.ToString();
            acad.EmployeeType = wb.Worksheet(sheet).Cell(row, 6).Value.ToString();
            acad.Periodo = wb.Worksheet(sheet).Cell(row, 7).Value.ToString();
            acad.Sigla = wb.Worksheet(sheet).Cell(row, 8).Value.ToString();
            acad.Paralelo = wb.Worksheet(sheet).Cell(row, 9).Value.ToString();
            acad.AcademicHoursWeek = strToDecimal(row,10);
            acad.AcademicHoursMonth = strToDecimal(row, 11);
            acad.IdentificadorPago = wb.Worksheet(sheet).Cell(row, 12).Value.ToString();
            acad.CategoriaDocente = wb.Worksheet(sheet).Cell(row, 13).Value.ToString();
            acad.CostoHora = strToDecimal(row, 14);
            acad.CostoMes = strToDecimal(row, 15);
            acad.CUNI = wb.Worksheet(sheet).Cell(row, 16).Value.ToString();
            acad.Dependency = wb.Worksheet(sheet).Cell(row, 17).Value.ToString();
            acad.PEI = wb.Worksheet(sheet).Cell(row, 18).Value.ToString();
            acad.SAPParaleloUnit = wb.Worksheet(sheet).Cell(row, 19).Value.ToString();
            
            acad.Matched = 0;
            acad.Porcentaje = 0.0m;
            acad.mes = this.mes;
            acad.gestion = this.gestion;
            acad.segmentoOrigen = this.segmentoOrigen;

            acad.DistFileId = file.Id;
            return acad;
        }
    }
}