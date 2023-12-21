using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Controllers;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Dist;
using System.Data.Entity;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Logic.ExcelFiles
{
    public class PayrollExcel:ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Carnet Identidad", typeof(string)), 
            new Excelcol("Primer Apellido", typeof(string)),
            new Excelcol("Segundo Apellido", typeof(string)),
            new Excelcol("Nombres", typeof(string)),
            new Excelcol("Apellido Casada", typeof(string)),
            new Excelcol("Haber Básico", typeof(double)),
            new Excelcol("Bono de Antigüedad", typeof(double)),
            new Excelcol("Otros Ingresos", typeof(double)),
            new Excelcol("Ingresos por docencia", typeof(double)),
            new Excelcol("Ingresos por otras actividades académicas", typeof(double)),
            new Excelcol("Reintegro", typeof(double)),
            new Excelcol("Total Ganado", typeof(double)),
            new Excelcol("Identificador de AFP", typeof(string)),
            new Excelcol("Aporte Laboral AFP", typeof(double)),
            new Excelcol("RC-IVA", typeof(double)),
            new Excelcol("Descuentos", typeof(double)),
            new Excelcol("Total Deducciones", typeof(double)),
            new Excelcol("Liquido Pagable", typeof(double)),
            new Excelcol("CUNI", typeof(string)),
            new Excelcol("Tipo empleado", typeof(string)),
            new Excelcol("PEI", typeof(string)),
            new Excelcol("Horas de trabajo mensual", typeof(double)),
            new Excelcol("Identificador de Dependencia", typeof(string)),
            new Excelcol("Aporte Patronal AFP", typeof(double)),
            new Excelcol("Identificador Seguridad Corto Plazo", typeof(string)),
            new Excelcol("Aporte Patronal SCP", typeof(double)),
            new Excelcol("Provisión Aguinaldos", typeof(double)),
            new Excelcol("Provisión Primas", typeof(double)),
            new Excelcol("Provisión Indemnización", typeof(double)),
            new Excelcol("Modo Pago", typeof(string))
        };

        private string mes, gestion, segmentoOrigen;
        private int segmentInt;
        private ApplicationDbContext _context;
        private Dist_File file;
        public PayrollExcel(Stream data, ApplicationDbContext context, string fileName, string mes, string gestion, string segmentoOrigen,Dist_File file, int headerin = 1, int sheets = 1, string resultfileName = "PayrollResult")
            : base(cols, data, fileName, headerin: headerin, resultfileName: resultfileName,sheets:sheets)
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
        public PayrollExcel(string fileName,int headerin = 1):base(cols,fileName,headerin)
        { }

        public override void toDataBase()
        {
            IXLRange UsedRange = wb.Worksheet(1).RangeUsed();

            for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber() ; i++)
            {
                _context.DistPayrolls.Add(ToDistPayroll(i));
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
            this.segmentInt = Int32.Parse(this.segmentoOrigen);
            bool v1 = VerifyColumnValueIn(13, connB1.getBusinessPartners().Cast<string>().ToList(), comment: "Esta AFP no esta registrada como un Bussines Partner en SAP");
            bool v2 = VerifyColumnValueIn(20, _context.TipoEmpleadoDists.Select(x => x.Name).ToList(), comment: "Este Tipo empleado no es valido.\n");
            bool v3 = VerifyColumnValueIn(21, connB1.getCostCenter(B1Connection.Dimension.PEI,mes:this.mes,gestion:this.gestion).Cast<string>().ToList(), comment: "Este PEI no existe en SAP.\n");
            bool v4 = VerifyColumnValueIn(23, _context.Dependencies.Where(x=>x.BranchesId == this.segmentInt).Select(m => m.Cod).Distinct().ToList(),comment:"Esta Dependencia no existe en la Base de Datos Nacional.\n");
            bool v5 = VerifyPerson(ci: 1, CUNI: 19, fullname: 2, personActive: true, branchesId:this.segmentInt, date: this.gestion + "-" + this.mes + "-01", dependency:23,paintdep:true,tipo:20);
            bool v6 = VerifyColumnValueIn(25, connB1.getBusinessPartners().Cast<string>().ToList(), comment: "Este seguro no esta registrado como un Bussines Partner en SAP");
            bool v7 = ValidateLiquidoPagable();
            bool v8 = ValidatenoZero();
            // Validar personas activas
            bool v9 = validateAllPeopleInPayroll();
            
            // HB jamás puede ser <=0
            bool v10 = ValidateHBIsNotZero(6);
            // Bono
            bool v11 = ValidateNoNegative(7);
            // Otros Ingresos
            bool v12 = ValidateNoNegative(8);
            // Ingresos Docencia
            bool v13 = ValidateNoNegative(9);
            // Ingresos Otras Actividades Academicas
            bool v14 = ValidateNoNegative(10);
            // Reintegro
            bool v15 = ValidateNoNegative(11);
            // Total Ganado
            bool v16 = ValidateNoNegative(12);
            // Aporte AFP
            bool v17 = ValidateNoNegative(14);
            // IVA
            bool v18 = ValidateNoNegative(15);
            // Descuentos
            bool v19 = ValidateNoNegative(16);
            // Total Descuentos
            bool v20 = ValidateNoNegative(17);
            // Liquido Pagable
            bool v21 = ValidateNoNegative(18);
            // Horas Trabajadas
            bool v22 = ValidateNoNegative(22);
            // Aporte Patronal AFP
            bool v23 = ValidateNoNegative(24);
            // Aporte Patronal SCP
            bool v24 = ValidateNoNegative(26);
            // Provision Aguinaldo
            bool v25 = ValidateNoNegative(27);
            // Provision Prima
            bool v26 = ValidateNoNegative(28);
            // Provision Indeminizacion
            bool v27 = ValidateNoNegative(29);
            // Socio de Negocio AFP
            bool v28 = ValidateSN(13);
            // Socio de Negocio SSU
            bool v29 = ValidateSN(25);
            bool Negativos = v11 && v12 && v13 && v14 && v15 && v16 && v17 && v18 && v19 && v20 && v21 && v22 &&
                             v23 && v24 && v25 && v26 && v27;
            // Cheque or Banco
            bool v30 = VerifyColumnValueIn(30,new List<string>(){"CHQ","BCO"},comment:"Este no es un tipo valido de modo de pago");
            return isValid() && v1 && v2 && v3 && v4 && v5 && v6  && v7 && v8 && v9 && v10 && Negativos && v28 && v29 && v30;
        }

        public bool ValidateSN(int col, int sheet = 1)
        {
            bool res = true;
            string comment = "Este Socio de Negocio no corresponde a la Regional.";
            int sn = Int32.Parse(this.segmentoOrigen);
            var reg = _context.Branch.FirstOrDefault(x => x.Id == sn);
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                if (!wb.Worksheet(sheet).Cell(i, col).Value.ToString().StartsWith(reg.InicialSN))
                {
                    res = false;
                    paintXY(col, i, XLColor.Red, comment);
                }
            }
            valid = valid && res;
            return res;
        }

        public bool ValidateNoNegative(int col, int sheet = 1)
        {
            bool res = true;
            string comment = "Este Valor no puede ser negativo.";
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                double nz = -1;
                if (Double.TryParse(wb.Worksheet(sheet).Cell(i, col).Value.ToString(), out nz))
                {
                    if (nz<0)
                    {
                        res = false;
                        paintXY(col, i, XLColor.Red, comment);
                    }
                }
            }
            valid = valid && res;
            if (!res)
                addError("Valor negativo", "Existen columnas que no pueden ser negativas, ni iguales a 0");
            return res;
        }


        public bool ValidateHBIsNotZero(int col, int sheet = 1)
        {
            bool res = true;
            string comment = "Este Valor no puede ser menor o igual 0.";
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                double nz = -1;
                // busca en todas las celdas de la columna enviada, y revisa que ninguna sea menor o igual a 0
                if (Double.TryParse(wb.Worksheet(sheet).Cell(i, col).Value.ToString(), out nz))
                {
                    if (nz <= 0)
                    {
                        // si se cumple la condicion, la pinta y marca el error de la variable comment
                        res = false;
                        paintXY(col, i, XLColor.Red, comment);
                    }
                }
            }
            valid = valid && res;
            if (!res)
                addError("Valor de haber básico menor o igual a 0", "La columna del haber básico no puede ser menor o igual a 0");
            return res;
        }
        

        public bool ValidatenoZero(int sheet = 1)
        {
            int tipo = 20;
            int nozero = 22;
            bool res = true;
            string comment = "Este Valor no puede ser cero.";
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                int nz = -1;
                if (wb.Worksheet(sheet).Cell(i, tipo).Value.ToString() != "TH" && Int32.TryParse(wb.Worksheet(sheet).Cell(i, nozero).Value.ToString(),out nz))
                {
                    if (nz == 0)
                    {
                        res = false;
                        paintXY(nozero, i, XLColor.Red, comment);
                    }
                }
            }
            valid = valid && res;
            if(!res)
                addError("Valor cero", "Existen columnas que no pueden ser cero");
            return res;
        }

        public bool ValidateLiquidoPagable(int sheet=1)
        {
            int i1 = 6, i2 = 7, i3 = 8, i4 = 9, i5 = 10, i6 =11;
            int d1 = 14, d2 = 15, d3 = 16;
            int errortg = 12, errortd = 17, errorlp = 18;

            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();

            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                decimal in1 = 0, in2 = 0, in3 = 0, in4 = 0, in5 = 0, in6 = 0,ti=0,lp=0;
                decimal de1 = 0, de2 = 0, de3 = 0, td=0;
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i1).Value.ToString(), out in1);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i2).Value.ToString(), out in2);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i3).Value.ToString(), out in3);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i4).Value.ToString(), out in4);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i5).Value.ToString(), out in5);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, i6).Value.ToString(), out in6);

                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, d1).Value.ToString(), out de1);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, d2).Value.ToString(), out de2);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, d3).Value.ToString(), out de3);

                var ingresos = Math.Round(in1, 2) + Math.Round(in2, 2) + Math.Round(in3, 2) + Math.Round(in4, 2) + Math.Round(in5, 2) + Math.Round(in6, 2);
                var descuentos = Math.Round(de1, 2) + Math.Round(de2, 2) + Math.Round(de3, 2);

                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString(), out ti);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, 17).Value.ToString(), out td);
                Decimal.TryParse(wb.Worksheet(sheet).Cell(i, 18).Value.ToString(), out lp);


                if (ingresos != ti)
                {
                    res = false;
                    paintXY(errortg, i, XLColor.Red, "No cuadran Ingresos. la suma sale: "+ingresos);
                    addError("No cuadran Ingresos", "La suma se calculó: " +ingresos + " se encontró " + ti);
                }

                if (descuentos != td)
                {
                    res = false;
                    paintXY(errortd, i, XLColor.Red, "No cuadran Descuentos. la suma sale: " + descuentos);
                    addError("No cuadran Descuentos", "La suma se calculó: " + descuentos + " se encontró " + td);
                }

                var dif = ingresos - descuentos;

                if ( dif!= lp)
                {
                    res = false;
                    paintXY(errorlp, i, XLColor.Red, "No cuadran Liquido Pagable. la suma sale: " + (ingresos-descuentos));
                    addError("No cuadran Liquido Pagable", "La suma se calculó: " + dif + " se encontró " + lp);
                }
            }

            valid = valid && res;
            return res;
        }

        private bool validateAllPeopleInPayroll()
        {
            var br = Int32.Parse(this.segmentoOrigen);
            var date = new DateTime(Int32.Parse(this.gestion), Int32.Parse(this.mes),1);
            //Actualizacion de validacion de gente activa al mes y año
            try
            {
                //query con la formula de la funcion PERSONAL AT DATE
                var active = _context.Database.SqlQuery<ContractDetail>("select * from (select a.* " +
                                                                        "\r\nfrom \""+CustomSchema.Schema+"\".\"ValidoSalomon\" vs" +
                                                                        "\r\ninner join \"" + CustomSchema.Schema + "\".\"ContractDetail\" a on a.\"Id\" = vs.\"ContractDetailId\"" +
                                                                        "\r\nwhere (a.\"EndDate\" is null and year(a.\"StartDate\")*100+month(a.\"StartDate\")" +
                                                                        "<=year('" + date.Year + '-' + date.Month + '-' + date.Day + "')*100+month('" + date.Year + '-' + date.Month + '-' + date.Day + "'))" +
                                                                        "\r\nor year('" + date.Year + '-' + date.Month + '-' + date.Day + "')*100+month('" + date.Year + '-' + date.Month + '-' + date.Day + "')" +
                                                                        " between year(a.\"StartDate\")*100+month(a.\"StartDate\") " +
                                                                        " and year(a.\"EndDate\")*100+month(a.\"EndDate\")) x where x.\"BranchesId\" =" + this.segmentoOrigen).ToList();
                IXLRange UsedRange = wb.Worksheet(1).RangeUsed();
                List<string> payrollCunis = new List<string>();
                // generate list 
                for (int i = 1 + headerin; i <= UsedRange.LastRow().RowNumber(); i++)
                {
                    payrollCunis.Add(wb.Worksheet(1).Cell(i, 19).Value.ToString());
                }
                var res = active.Where(x => !payrollCunis.Contains(x.CUNI));
                var str = "Las siguientes personas se encuentran activas en el sistema pero no se las registró en planillas:";
                //Sacamos el nombre de las personas que no estan incluidas en la planilla
                    foreach (var p in res)
                    {
                        //str += "\n -" + p.People.GetFullName();
                        str += "\n -" + _context.Person.FirstOrDefault(x => x.CUNI == p.CUNI).GetFullName();
                    }
                    if (res.Count() > 0)
                        addError("Personas Faltantes", str);
                    return res.Count() == 0;
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        public Dist_Payroll ToDistPayroll(int row, int sheet = 1)
        {
            Dist_Payroll payroll = new Dist_Payroll();
            payroll.Id = Dist_Payroll.GetNextId(_context);
            payroll.Document = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            payroll.Names = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            payroll.FirstSurName = wb.Worksheet(sheet).Cell(row, 3).Value.ToString();
            payroll.SecondSurName = wb.Worksheet(sheet).Cell(row, 4).Value.ToString();
            payroll.MariedSurName = wb.Worksheet(sheet).Cell(row, 5).Value.ToString();
            payroll.BasicSalary = Math.Round(strToDecimal(row, 6), 2);
            payroll.AntiquityBonus = Math.Round(strToDecimal(row, 7), 2);
            payroll.OtherIncome = Math.Round(strToDecimal(row, 8), 2);
            payroll.TeachingIncome = Math.Round(strToDecimal(row, 9), 2);
            payroll.OtherAcademicIncomes = Math.Round(strToDecimal(row, 10), 2);
            payroll.Reintegro = Math.Round(strToDecimal(row, 11), 2);
            payroll.TotalAmountEarned = Math.Round(strToDecimal(row, 12), 2);
            payroll.AFP = wb.Worksheet(sheet).Cell(row, 13).Value.ToString();
            payroll.AFPLaboral = Math.Round(strToDecimal(row, 14), 2);
            payroll.RcIva = Math.Round(strToDecimal(row, 15), 2);
            payroll.Discounts = Math.Round(strToDecimal(row, 16), 2);
            payroll.TotalAmountDiscounts = Math.Round(strToDecimal(row, 17), 2);
            payroll.TotalAfterDiscounts = Math.Round(strToDecimal(row, 18), 2);
            payroll.CUNI = wb.Worksheet(sheet).Cell(row, 19).Value.ToString();
            payroll.EmployeeType = wb.Worksheet(sheet).Cell(row, 20).Value.ToString();
            payroll.PEI = wb.Worksheet(sheet).Cell(row, 21).Value.ToString();
            payroll.WorkedHours = strToDouble(row, 22);
            payroll.Dependency = wb.Worksheet(sheet).Cell(row, 23).Value.ToString();
            payroll.AFPPatronal = Math.Round(strToDecimal(row, 24), 2);
            payroll.IdentificadorSSU = wb.Worksheet(sheet).Cell(row, 25).Value.ToString();
            payroll.SeguridadCortoPlazoPatronal = Math.Round(strToDecimal(row, 26), 2);
            payroll.ProvAguinaldo = Math.Round(strToDecimal(row, 27), 2);
            payroll.ProvPrimas = Math.Round(strToDecimal(row, 28), 2);
            payroll.ProvIndeminizacion = Math.Round(strToDecimal(row, 29), 2);
            payroll.ProcedureTypeEmployee = payroll.EmployeeType;
            payroll.ModoPago = wb.Worksheet(sheet).Cell(row, 30).Value.ToString();

            payroll.Porcentaje = 0m;
            payroll.mes = this.mes;
            payroll.gestion = this.gestion;
            payroll.segmentoOrigen = this.segmentoOrigen;
            payroll.DistFileId = file.Id;
            return payroll;
        }
    }
}