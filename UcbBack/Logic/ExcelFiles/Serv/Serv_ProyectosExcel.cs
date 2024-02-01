using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using ClosedXML.Excel;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Auth;
using UcbBack.Models.Serv;
using System.Configuration;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System.Globalization;
using System.Data.Entity;
using Newtonsoft.Json.Linq;
using UcbBack.Models.Dist;

namespace UcbBack.Logic.ExcelFiles.Serv
{
    public class Serv_ProyectosExcel : ValidateExcelFile
    {
        private static Excelcol[] cols = new[]
        {
            new Excelcol("Codigo Socio", typeof(string)), 
            new Excelcol("Nombre Socio", typeof(string)),
            new Excelcol("Cod Dependencia", typeof(string)),
            new Excelcol("PEI PO", typeof(string)),
            new Excelcol("Nombre del Servicio", typeof(string)),
            new Excelcol("Codigo Proyecto SAP", typeof(string)),
            new Excelcol("Nombre del Proyecto", typeof(string)),
            new Excelcol("Version", typeof(string)),
            new Excelcol("Periodo Academico", typeof(string)),
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

        public Serv_ProyectosExcel(string fileName, int headerin = 1)
            : base(cols, fileName, headerin)
        { }

        public Serv_ProyectosExcel(Stream data, ApplicationDbContext context, string fileName, ServProcess process, CustomUser user, int headerin = 1, int sheets = 1, string resultfileName = "Result")
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
                _context.ServProyectoses.Add(ToServVarios(i));
            }

            _context.SaveChanges();
        }

        public Serv_Proyectos ToServVarios(int row, int sheet = 1)
        {
            Serv_Proyectos data = new Serv_Proyectos();
            data.Id = Serv_Proyectos.GetNextId(_context);

            data.CardCode = wb.Worksheet(sheet).Cell(row, 1).Value.ToString();
            data.CardName = wb.Worksheet(sheet).Cell(row, 2).Value.ToString();
            var cod = wb.Worksheet(sheet).Cell(row, 3).Value.ToString();
            var depId = _context.Dependencies
                .FirstOrDefault(x => x.Cod == cod);
            data.DependencyId = depId.Id;
            data.PEI = wb.Worksheet(sheet).Cell(row, 4).Value.ToString();
            data.ServiceName = wb.Worksheet(sheet).Cell(row, 5).Value.ToString();
            data.ProjectSAPCode = wb.Worksheet(sheet).Cell(row, 6).Value.ToString();
            data.ProjectSAPName = wb.Worksheet(sheet).Cell(row, 7).Value.ToString();
            data.Version = wb.Worksheet(sheet).Cell(row, 8).Value.ToString();
            data.Periodo = wb.Worksheet(sheet).Cell(row, 9).Value.ToString();
            data.AssignedJob = wb.Worksheet(sheet).Cell(row, 10).Value.ToString();

            data.AssignedAccount = wb.Worksheet(sheet).Cell(row, 11).Value.ToString();
            data.ContractAmount = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 12).Value.ToString());
            data.IUE = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 13).Value.ToString());
            data.IT = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 14).Value.ToString());
            data.IUEExterior = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 15).Value.ToString());
            data.TotalAmount = Decimal.Parse(wb.Worksheet(sheet).Cell(row, 16).Value.ToString());
            data.Comments = wb.Worksheet(sheet).Cell(row, 17).Value.ToString();
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
                    addError("Error en SAP", "No se puedo conectar con SAP B1, es posible que algunas validaciones cruzadas con SAP no sean ejecutadas");
                }
                // Verifica el Socio de negocio CODIGO_SOCIO
                bool v1 = VerifyBP(1, 2, process.BranchesId, user);
                // Verifica la existencia de la dependencia COD_DEPENDENCIA
                bool v2 = VerifyColumnValueIn(3, _context.Dependencies.Where(x => x.BranchesId == this.process.BranchesId).Select(x => x.Cod).ToList(), comment: "Esta Dependencia no es Válida");
                // Declara las variables de validacion dependientes de dependencia
                bool v5 = false, v11 = false, v13 = false;
                // Si dependencia es TRUE recien valida
                if (v2)
                {
                    // Si la dependencia esta equivocada no se pueden realizar ninguna de estas validaciones porque salga NULL EXCEPTION
                    // Verificar existencia del proyecto
                    v5 = verifyproject(dependency: 3);
                    if (v5)
                    {
                        // Verificar si el rango de fecha del proyecto es valida
                        v11 = verifyDates(dependency: 3);
                        // Verificar si el PEI registrado es correcto con el PEI del Proyecto en SAP
                        v13 = verifyProjectPei(6, 4);
                    }
                }
                var pei = connB1.getCostCenter(B1Connection.Dimension.PEI).Cast<String>().ToList();
                // Existencia del PEI
                bool v3 = VerifyColumnValueIn(4, pei, comment: "Este PEI no existe en SAP.");
                // Fechas validas para el PEI
                bool v15 = VerifyColumnValueIn(4, connB1.getCostCenter(B1Connection.Dimension.PEI, mes: DateTime.Now.ToString("MM"), gestion: DateTime.Now.ToString("yyyy")).Cast<string>().ToList(), comment: "Este PEI se encuentra vencido.");
                // Verifica que la columna NOMBRE DEL SERVICIO sea menor o igual a 50
                bool v4 = VerifyLength(5, 50);
                // Verifica que la columna NOMBRE DEL PROYECTO sea igual o menor a 40
                bool v12 = VerifyLength(7, 40);
                var periodo = connB1.getCostCenter(B1Connection.Dimension.Periodo).Cast<string>().ToList();
                periodo.Add("");
                // Validación de existencia del periodo en SAP
                bool v6 = VerifyColumnValueIn(9, periodo, comment: "Este Periodo no existe en SAP.");
                // Verifica la existencia de la tarea en la tabla maestra de Tareas
                bool v7 = VerifyColumnValueIn(10, ConversionStringList("select \"Abr\" \"Name\"  from " + CustomSchema.Schema + ".\"TipoTarea\";"), comment: "No existe este tipo de Tarea Asignada.");
                // Verifica que la cuenta asignada este entre las cuentas existentes que tienen la bandera "VerificacionProyecto"
                bool v8 = VerifyColumnValueInWithSpace(11, ConversionStringList("select \"Name\" from " + CustomSchema.Schema + ".\"GrupoContable\" where  \"VerificacionProyecto\" = true;"), comment: "No existe este tipo de Cuenta Asignada.");
                //Nueva validación para comprobar que la cuenta asignada corresponde al proyecto
                bool v9 = true;
                // Verifica que ninguna de las columnas ingrese vacia
                foreach (var i in new List<int>() { 1, 2, 3, 4, 5, 7, 10, 11, 12, 16 })
                {
                    v9 = VerifyNotEmpty(i) && v9;
                }
                // Verifica que los montos cuadren
                bool v10 = VerifyTotal2();
                //Verifica que el comentario solo tenga 300 caracteres
                bool v14 = VerifyLength(16, 300);
                bool v20 = verifyTipoDocente(process.TipoDocente);

                return v1 && v2 && v3 && v4 && v5 && v6 && v7 && v8 && v9 && v12 && v13 && v15 && v10 && v11 && v14 && v20;
            }

            return false;

        }

        private bool verifyTipoDocente(string tipoDocente)
        {
            bool res = true;
            int sheet = 1;
            string td = tipoDocente;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()), 2);
                decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 14).Value.ToString()), 2);
                decimal IUEExterior = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 15).Value.ToString()), 2);

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

        private bool verifyproject(int dependency, int sheet = 1)
        {
            string commnet = "Este proyecto no existe en SAP.";
            var connB1 = B1Connection.Instance();
            var br = _context.Branch.FirstOrDefault(x => x.Id == process.BranchesId);
            var list = connB1.getProjects("*").Where(x => x.U_Sucursal == br.Abr).Select(x => new { x.PrjCode, x.U_UORGANIZA }).ToList();
            int index = 6;
            int tipoproy = 11;
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();

            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var strproject = index != -1 ? wb.Worksheet(sheet).Cell(i, index).Value.ToString() : null;
                var strdependency = dependency != -1 ? wb.Worksheet(sheet).Cell(i, dependency).Value.ToString() : null;
                var dep = _context.Dependencies.Where(x => x.BranchesId == br.Id).Include(x => x.OrganizationalUnit).FirstOrDefault(x => x.Cod == strdependency);
                //------------------------------------Valida existencia del proyecto--------------------------------
                //Si no existe en esta rama un proyecto que haga match con el proyecto del excel
                if (!list.Exists(x => string.Equals(x.PrjCode.ToString(), strproject, StringComparison.OrdinalIgnoreCase)))
                {
                    //Si el tipo de proyecto, no es de los siguientes tipos y el codigo del proyecto no viene vacío
                    if (!(
                        (
                            wb.Worksheet(sheet).Cell(i, tipoproy).Value.ToString() == "CC_EC"
                            || wb.Worksheet(sheet).Cell(i, tipoproy).Value.ToString() == "CC_FC"
                            || wb.Worksheet(sheet).Cell(i, tipoproy).Value.ToString() == "CC_SA"
                        )
                        &&
                        wb.Worksheet(sheet).Cell(i, index).Value.ToString() == ""
                    ))
                    {
                        res = false;
                        paintXY(index, i, XLColor.Red, commnet);
                    }
                }
                else
                {
                    //como ya sabemos que existe el proyecto, ahora preguntamos de la UO
                    //dep es de la celda correcta
                    var row = list.FirstOrDefault(x => x.PrjCode == strproject);
                    string UO = row.U_UORGANIZA.ToString();
                    string UOName = "NO EXISTE";
                    try
                    {
                        UOName = _context.OrganizationalUnits.FirstOrDefault(x => x.Cod == UO).Name;
                    }
                    catch
                    {
                        UOName = "NO EXISTE";
                    }

                    if (!string.Equals(dep.OrganizationalUnit.Cod.ToString(), row.U_UORGANIZA.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        //Si la UO para esta fila es diferente de la UO registrada en SAP, marcamos error
                        res = false;
                        paintXY(dependency, i, XLColor.Red, "Este proyecto debe tener una dependencia asociada a la Unidad Org: " + row.U_UORGANIZA + " " + UOName);
                    }

                }
            }
            valid = valid && res;
            if (!res)
            {
                addError("Valor no valido", "Proyecto o proyectos no validos en la columna: " + index, false);
            }

            return res;
        }
        /*
       private bool verifyAccounts(int dependency, int sheet = 1)
       {
           string commnet;//especifica el error
           var connB1 = B1Connection.Instance();
           var br = _context.Branch.FirstOrDefault(x => x.Id == process.BranchesId);
           //todos los proyectos de esa rama
           var list = connB1.getProjects("*").Where(x => x.U_Sucursal == br.Abr).Select(x => new { x.PrjCode, x.U_Tipo, x.ValidFrom, x.ValidTo, x.U_UORGANIZA }).ToList();
           //columnas del excel
           int index = 6;
           int tipoProyecto = 11;
           bool res = true;
           int badAccount = 0;
           int badType = 0;
           IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
           for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
           {
               if (list.Exists(x => string.Equals(x.PrjCode.ToString(), wb.Worksheet(sheet).Cell(i, index).Value.ToString(), StringComparison.OrdinalIgnoreCase)))
               {
                   if (wb.Worksheet(sheet).Cell(i, tipoProyecto).Value.ToString() != "CAP")
                   {
                        //-----------------------------Validaciones de la cuenta--------------------------------
                       var tiposBD = _context.TableOfTableses.Where(x => x.Type.Equals("TIPOS_P&C_SARAI")).Select(x => x.Value).ToList();
                       var projectType = list.Where(x => x.PrjCode == wb.Worksheet(sheet).Cell(i, index).Value.ToString()).FirstOrDefault().U_Tipo.ToString();//tipo de proyecto del proyecto en la celda
                       string tipo = projectType;//variable auxiliar, no puede usarse la de arriba en EF por ser dinámica
                       var typeExists = tiposBD.Exists(x => string.Equals(x.Split(':')[0], tipo, StringComparison.OrdinalIgnoreCase));
                       //el tipo de proyecto existe en nuestra tabla de tablas?
                       if (!typeExists)
                       {
                           commnet = "El tipo de proyecto: " + tipo + " no es válido.";
                           paintXY(index, i, XLColor.Red, commnet);
                           res = false;
                           badType++;
                       }
                       else
                       {
                           var projectAccount = wb.Worksheet(sheet).Cell(i, tipoProyecto).Value.ToString();
                           var assignedAccount = tiposBD.Where(x => x.Split(':')[0].Equals(tipo)).FirstOrDefault().ToString().Split(':')[1];
                           if (projectAccount != assignedAccount)
                           {
                               commnet = "La cuenta asignada es incorrecta, debería ser: " + assignedAccount;
                               paintXY(tipoProyecto, i, XLColor.Red, commnet);
                               res = false;
                               badAccount++;
                           }
                       }
                   }
                   
               }
           }
           valid = valid && res;
           if (!res && badAccount > 0 && badType > 0) { addError("Valor no valido", "Tipos de proyectos no válidos en la columna: " + index + " y cuentas asignadas no válidas en la columna: " + tipoProyecto, false); }
           else if (!res && badAccount > 0 && badType == 0) { addError("Valor no valido", "Cuentas asignadas no válidas en la columna: " + tipoProyecto, false); }
           else if (!res && badAccount == 0 && badType > 0) { addError("Valor no valido", "Tipos de proyectos no válidos en la columna: " + index, false); }
           
           return res;
       }
        */
        private bool verifyDates(int dependency, int sheet = 1)
        {
            string commnet;//especifica el error
            var connB1 = B1Connection.Instance();
            var br = _context.Branch.FirstOrDefault(x => x.Id == process.BranchesId);
            //todos los proyectos de esa rama
            var list = connB1.getProjects("*").Where(x => x.U_Sucursal == br.Abr).Select(x => new { x.PrjCode, x.U_Tipo, x.ValidFrom, x.ValidTo, x.U_UORGANIZA }).ToList();
            //columnas del excel
            int index = 6;
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();

            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                if (list.Exists(x => string.Equals(x.PrjCode.ToString(), wb.Worksheet(sheet).Cell(i, index).Value.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    var strproject = index != -1 ? wb.Worksheet(sheet).Cell(i, index).Value.ToString() : null;
                    var row = list.FirstOrDefault(x => x.PrjCode == strproject);
                    string UO = row.U_UORGANIZA.ToString();
                    var strdependency = dependency != -1 ? wb.Worksheet(sheet).Cell(i, dependency).Value.ToString() : null;
                    var dep = _context.Dependencies.Where(x => x.BranchesId == br.Id).Include(x => x.OrganizationalUnit).FirstOrDefault(x => x.Cod == strdependency);
                    //Si la UO hace match también
                    if (row.U_UORGANIZA == dep.OrganizationalUnit.Cod)
                    {
                        //-----------------------------Validaciones de la fecha del proyecto--------------------------------
                        var projectInitialDate = list.Where(x => x.PrjCode == wb.Worksheet(sheet).Cell(i, index).Value.ToString()).FirstOrDefault().ValidFrom.ToString();
                        DateTime parsedIni = Convert.ToDateTime(projectInitialDate);
                        var projectFinalDate = list.Where(x => x.PrjCode == wb.Worksheet(sheet).Cell(i, index).Value.ToString()).FirstOrDefault().ValidTo.ToString();
                        DateTime parsedFin = Convert.ToDateTime(projectFinalDate);

                        //si el tiempo actual es menor al inicio del proyecto en SAP ó si el tiempo actual es mayor a la fecha límite del proyectoSAP
                        if (System.DateTime.Now < parsedIni || System.DateTime.Now > parsedFin)
                        {
                            res = false;
                            commnet = "La fecha de este proyecto ya está cerrada, estuvo disponible del " + parsedIni + " al " + parsedFin;
                            paintXY(index, i, XLColor.Red, commnet);
                        }
                    }
                }
            }
            valid = valid && res;
            if (!res) { addError("Valor no valido", "Proyecto/s con fechas no válidas en la columna:" + index, false); }

            return res;
        }

        private bool verifyProjectPei(int proy, int pei, int sheet = 1)
        {
            string commnet;//especifica el error
            var connB1 = B1Connection.Instance();
            var br = _context.Branch.FirstOrDefault(x => x.Id == process.BranchesId);
            //todos los proyectos de esa rama
            var list = connB1.getProjects("*").Where(x => x.U_Sucursal == br.Abr).Select(x => new { x.PrjCode, x.U_Tipo, x.ValidFrom, x.ValidTo, x.U_UORGANIZA, x.U_PEI_PO }).ToList();
            //columnas del excel
            bool res = true;
            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            var l = UsedRange.LastRow().RowNumber();


            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                var pr = connB1.getProjects("*").Where(x => x.U_Sucursal == br.Abr).Where(x => x.PrjCode == wb.Worksheet(sheet).Cell(i, proy).Value.ToString()).Select(x => new { x.PrjCode, x.U_Tipo, x.ValidFrom, x.ValidTo, x.U_UORGANIZA, x.U_PEI_PO }).ToList();
                if (wb.Worksheet(sheet).Cell(i, proy).Value.ToString().Equals(""))
                {
                    res = true;
                }
                else
                {
                    if (!pr.Exists(x => string.Equals(x.U_PEI_PO.ToString(), wb.Worksheet(sheet).Cell(i, pei).Value.ToString(), StringComparison.OrdinalIgnoreCase)))
                    {
                        res = false;
                        var similarities = pr.Where(x => x.PrjCode == wb.Worksheet(sheet).Cell(i, proy).Value.ToString()).Select(x => new { x.PrjCode, x.U_Tipo, x.ValidFrom, x.ValidTo, x.U_UORGANIZA, x.U_PEI_PO }).ToList().FirstOrDefault();

                        commnet = "Este PEI no es correspondiente al proyecto registrado. No será '" + similarities.U_PEI_PO + "'?";
                        paintXY(pei, i, XLColor.Red, commnet);
                    }
                }
            }
            valid = valid && res;
            if (!res) { addError("Valor no valido", "Proyecto/s con PEI no válidos en la columna:" + pei, false); }

            return res;
        }
        //private bool VerifyTotal()
        //{
        //    bool res = true;
        //    int sheet = 1;

        //    IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
        //    for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
        //    {
        //        //Necesitamos truncar a 2 decimales, no redondear
        //        decimal contrato = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString()), 2);
        //        decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()),2);
        //        decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 14).Value.ToString()),2);
        //        decimal total = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 15).Value.ToString()),2);

        //        if (contrato - IUE - IT != total)
        //        {
        //            res = false;
        //            paintXY(12, i, XLColor.Red, "Este valor no cuadra (Contrato - IUE - IT != Monto a Pagar)");
        //        }
        //    }

        //    valid = valid && res;
        //    if (!res)
        //        addError("Valor no valido", "Monto a Pagar no cuadra.", false);
        //    return res;
        //}

        private bool VerifyTotal2()
        {
            bool res = true;
            int sheet = 1;

            IXLRange UsedRange = wb.Worksheet(sheet).RangeUsed();
            for (int i = headerin + 1; i <= UsedRange.LastRow().RowNumber(); i++)
            {
                //Necesitamos truncar a 2 decimales, no redondear
                decimal contrato = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 12).Value.ToString()), 2);
                decimal IUE = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 13).Value.ToString()), 2);
                decimal IT = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 14).Value.ToString()), 2);
                decimal IUEExterior = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 15).Value.ToString()), 2);
                decimal total = TruncateDecimal(Decimal.Parse(wb.Worksheet(sheet).Cell(i, 16).Value.ToString()), 2);

                if (IUEExterior == 0)
                {
                    if (contrato - IUE - IT != total)
                    {
                        res = false;
                        paintXY(12, i, XLColor.Red, "Este valor no cuadra (Contrato - IUE - IT != Monto a Pagar para independientes)");
                    }
                }
                else
                {
                    if (contrato - IUEExterior != total)
                    {
                        res = false;
                        paintXY(12, i, XLColor.Red, "Este valor no cuadra (Contrato - IUEExterior != Monto a Pagar para extranjeros)");
                    }
                }
            }

            valid = valid && res;
            if (!res)
                addError("Valor no valido", "Monto a Pagar no cuadra.", false);
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