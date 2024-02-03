using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Auth;
using UcbBack.Models.Dist;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Serv;

namespace UcbBack.Logic.ExcelFiles.Serv
{
    public class Serv_CarreraExcel : ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Codigo Socio", typeof(string)), 
            new Excelcol("Nombre Socio", typeof(string)),
            new Excelcol("Cod Dependencia", typeof(string)),
            new Excelcol("PEI PO", typeof(string)),
            new Excelcol("Nombre del Servicio", typeof(string)),
            new Excelcol("Codigo Carrera", typeof(string)),
            new Excelcol("Documento Base", typeof(string)),
            new Excelcol("Postulante", typeof(string)),
            new Excelcol("Tipo Tarea Asignada", typeof(string)),
            new Excelcol("Cuenta Asignada", typeof(string)),
            new Excelcol("Monto Contrato", typeof(double)),
            new Excelcol("Monto IUE", typeof(double)),
            new Excelcol("Monto IT", typeof(double)),
            new Excelcol("IUEExterior", typeof(double)),
            new Excelcol("Monto a Pagar", typeof(double)),
            new Excelcol("Observaciones", typeof(string)),
        };

        private ApplicationDbContext _context;
        private ServProcess process;
        private CustomUser user;

        public Serv_CarreraExcel(string fileName, int headerin = 1)
            : base(cols, fileName, headerin)
        { }

        public Serv_CarreraExcel(Stream data, ApplicationDbContext context, string fileName, ServProcess process,CustomUser user, int headerin = 1, int sheets = 1, string resultfileName = "Result")
            : base(cols, data, fileName, headerin, sheets, resultfileName, context)
        {
            this.process = process;
            this.user = user;
            _context = context;
            isFormatValid();
        }

        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();

            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                _context.ServCarreras.Add(ToServVarios(i));
            }

            _context.SaveChanges();
        }

        public Serv_Carrera ToServVarios(int row, int sheet = 1)
        {
            Serv_Carrera data = new Serv_Carrera();
            data.Id = Serv_Carrera.GetNextId(_context);

            data.CardCode = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            data.CardName = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            var cod = wb.Worksheet(sheet).Cell(row, 3).Value.ToString();
            var depId = _context.Dependencies
                .FirstOrDefault(x => x.Cod == cod);
            data.DependencyId = depId.Id;
            data.PEI = wb.Worksheet(sheet).Cell(row, 4).Value.ToString();
            data.ServiceName = wb.Worksheet(sheet).Cell(row, 5).Value.ToString();
            data.Carrera = wb.Worksheet(sheet).Cell(row, 6).Value.ToString();
            data.DocumentNumber = wb.Worksheet(sheet).Cell(row, 7).Value.ToString();
            data.Student = wb.Worksheet(sheet).Cell(row, 8).Value.ToString();
            data.AssignedJob = wb.Worksheet(sheet).Cell(row, 9).Value.ToString();

            data.AssignedAccount = wb.Worksheet(sheet).Cell(row, 10).Value.ToString();
            data.ContractAmount = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 11).Value.ToString());
            data.IUE = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 12).Value.ToString());
            data.IT = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 13).Value.ToString());
            data.IUEExterior = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 14).Value.ToString());
            data.TotalAmount = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 15).Value.ToString());
            data.Comments = wb.Worksheet(sheet).Cell(row, 16).Value.ToString();
            data.Serv_ProcessId = process.Id;
            return data;
        }

        public List<string> ConversionStringList(string query)
        {
            var result = _context.Database.SqlQuery<Auxiliar>(query).ToList();
            int cant = result.Count;
            List<string> aux = new List<string>();
            for (int i = 0; i < cant; i++)
            {
                aux.Add(result[i].Name);
            }
            return aux;
        }
        public override bool ValidateFile()
        {
            if (isValid())
            {
                var connB1 = B1Connection.Instance();

                if (!connB1.connectedtoHana)
                {
                    addError("ErrorenSAP", "No se puede conectar con SAP B1, es posible que algunas validaciones cruzadas con SAP no sean ejecutadas");
                }
                // Verifica que exista Socio de Negocio
                bool v1 = VerifyBP(1, 2,process.BranchesId,user);
                // Verifica existencia de dependencia
                bool v2 = VerifyColumnValueIn(3, _context.Dependencies.Where(x => x.BranchesId == this.process.BranchesId).Select(x => x.Cod).ToList(), comment: "Esta Dependencia no es Válida");
                // Si la dependencia es equivocada no puede realizar validacion
                bool v11 = true;
                if (v2)
                {
                    // Verifica que la carrera tenga concondancia con la dependencia
                    v11 = VerifyCareer(cod: 6, branch: process.BranchesId, dependency: 3, sheet: 1);
                }
                var pei = connB1.getCostCenter(B1Connection.Dimension.PEI).Cast<string>().ToList();
                // Verifica existencia del PEI
                bool v3 = VerifyColumnValueIn(4, pei, comment: "Este PEI no existe en SAP.");
                // Verifica la cantidad de caracteres de NOMBRE DEL SERVICIO sea menor o igual a 50
                bool v4 = VerifyLength(5, 50);
                var brs = _context.Branch.FirstOrDefault(x => x.Id == process.BranchesId);
                var carrera = connB1.getCostCenter(B1Connection.Dimension.PlanAcademico).Cast<string>().ToList().Where(x => x.Contains(brs.Abr)).ToList();
                // Verifica que la carrera exista
                bool v5 = VerifyColumnValueIn(6, carrera, comment: "Esta Carrera no existe en SAP.");
                // Verifica que POSTULANTE sea menor o igual a 38
                bool v6 = VerifyLength(8, 38);
                // Verifica que la Tarea exista en BDD
                bool v7 = VerifyColumnValueIn(9, ConversionStringList("select \"Abr\" \"Name\"  from " + CustomSchema.Schema + ".\"TipoTarea\";"), comment: "No existe esta tipo de Tarea Asignada.");
                // Verifica que el Grupo Contable exista con la Bandera VerificacionSarai
                bool v8 = VerifyColumnValueInWithSpace(10, ConversionStringList("select \"Name\" from " + CustomSchema.Schema + ".\"GrupoContable\" where  \"VerificacionSarai\" = true;"), comment: "No existe este tipo de Cuenta Asignada.");
                // Verifica que las restas de montos cuadren
                bool v9 = VerifyTotal2();
                bool v10 = true;
                // Verifica que ninguna columna entre vacia
                foreach (var i in new List<int>() { 1, 2, 3, 4, 5, 6,/* 7, */8, 9, 10, 11, 15 })
                {
                    v10 = VerifyNotEmpty(i) && v10;
                }
                //Verifica que el comentario solo tenga 300 caracteres
                bool v12 = VerifyLength(15, 300);
                bool v20 = verifyTipoDocente();
                return v1 && v2 && v3 && v4 && v5 && v6 && v7 && v8 && v10 && v11 && v9 && v12 && v20;
            }

            return false;
        }
        private bool verifyTipoDocente()
        {
            bool res = true;
            int sheet = 1;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString()), 2);
                decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()), 2);
                decimal IUEExterior = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 14).Value.ToString()), 2);

                if (IUEExterior > 0 && process.TipoDocente == "INDEP")
                {
                    res = false;
                    paintXY(15, i, XLColor.Red, "Subió un archivo de extranjero como tipo de docente independiente");
                }
                if (IUE > 0 && IT > 0 && process.TipoDocente == "EXT")
                {
                    res = false;
                    paintXY(13, i, XLColor.Red, "Subió un archivo de independiente como tipo de docente extranjero");
                }
            }
            valid = valid && res;
            if (!res)
            {
                addError("Error de archivo", "Subió un archivo de un tipo de docente erróneo");
            }
            return res;
        }
        private bool VerifyTotal()
        {
            bool res = true;
            int sheet = 1;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                decimal contrato = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 11).Value.ToString()), 2);
                decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString()), 2);
                decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()), 2);
                decimal total = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 15).Value.ToString()), 2);
                if (contrato - IUE - IT != total)
                {
                    res = false;
                    paintXY(11, i, XLColor.Red, "Este valor no cuadra (Contrato - IUE - IT != Monto a Pagar)");
                }
            }

            valid = valid && res;
            if (!res)
                addError("ValorNoValido", "Monto a Pagar no cuadra.", false);
            return res;
        }

        private bool VerifyTotal2()
        {
            bool res = true;
            int sheet = 1;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                decimal contrato = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 11).Value.ToString()), 2);
                decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString()), 2);
                decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()), 2);
                decimal IUEExterior = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 14).Value.ToString()), 2);
                decimal total = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 15).Value.ToString()), 2);

                if (IUEExterior == 0)
                {
                    if (contrato - IUE - IT != total)
                    {
                        res = false;
                        paintXY(11, i, XLColor.Red, "Este valor no cuadra (Contrato - IUE - IT != Monto a Pagar para independientes)");
                    }
                }
                else
                {
                    if (contrato - IUEExterior != total)
                    {
                        res = false;
                        paintXY(11, i, XLColor.Red, "Este valor no cuadra (Contrato - IUEExterior != Monto a Pagar para extranjeros)");
                    }
                }
            }

            valid = valid && res;
            if (!res)
                addError("ValorNoValido", "Monto a Pagar no cuadra.", false);
            return res;
        }

        public decimal TruncateDecimal(decimal value, int precision)
        {
            decimal step = (decimal)Math.Pow(10, precision);
            decimal tmp = Math.Truncate(step * value);
            return tmp / step;
        }
    }
}