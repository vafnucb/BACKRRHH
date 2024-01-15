using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json.Linq;
using Sap.Data.Hana;
using SAPbobsCOM;
using UcbBack.Models;
using UcbBack.Models.Auth;
using UcbBack.Models.Dist;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using UcbBack.Models.Serv;
using Resource = SAPbobsCOM.Resource;

namespace UcbBack.Logic.B1
{
    public class B1Connection
    {
        private static object Lock = new Object();
        private static B1Connection instance = null;
        private struct BusinessObjectType
        {
            public static string BussinesPartner = "BUSINESSPARTNER";
            public static string BussinesPartnerCivil = "BUSINESSPARTNERCIVIL";
            public static string Voucher = "VOUCHER";
            public static string Employee = "EMPLOYEE";
        }

        private Company company;
        private HanaConnection HanaConn;
        private int connectionResult;
        private int errorCode = 0;
        private string errorMessage = "";
        private string DatabaseName;
        public bool connectedtoHana = false;
        public bool connectedtoB1 = false;
        private ApplicationDbContext _context;

        public enum Dimension
        {
            All,
            OrganizationalUnit,
            PEI,
            PlanAcademico,
            Paralelo,
            Periodo
        };


        private B1Connection()
        {
            try
            {
                DatabaseName = ConfigurationManager.AppSettings["HanaBD"];
                //string cadenadeconexion = "Server=192.168.18.180:30015;UserID=admnalrrhh;Password=Rrhh12345;Current Schema="+DatabaseName;
                //string cadenadeconexion = "Server=SAPHANA01:30015;UserID=SDKRRHH;Password=Rrhh1234;Current Schema=UCBTEST"+DatabaseName;
                string cadenadeconexion = "Server=" + ConfigurationManager.AppSettings["B1Server"] +
                                          ";UserID=" + ConfigurationManager.AppSettings["HanaBDUser"] +
                                          ";Password=" + ConfigurationManager.AppSettings["HanaPassword"] +
                                          ";Current Schema=" + ConfigurationManager.AppSettings["HanaBD"];
                HanaConn = new HanaConnection(cadenadeconexion);
                HanaConn.Open();
                connectedtoHana = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                connectedtoHana = false;
            }

            try
            {
                ConnectB1();
                connectedtoB1 = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                connectedtoB1 = false;
            }
            _context = new ApplicationDbContext();
            if (!connectedtoB1 && !connectedtoHana)
                instance = null;
        }

        // Double Check locking implementation for thread safe singleton
        public static B1Connection Instance()
        {
            if (instance == null) // 1st check
            {
                lock (Lock) // locked
                {
                    if (instance == null) // second check
                    {
                        instance = new B1Connection(); // instantiate a new (and the only one) instance
                    }
                }
            }

            return instance; // return the instance 
        }

        private bool DisconnectB1()
        {
            bool conectado = true;
            try
            {
                conectado = company.Connected;
                if (conectado)
                {
                    if (company.InTransaction)
                        company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                company.Disconnect();
                conectado = company.Connected;
            }
            catch
            { }
            return conectado;
        }

        private int ConnectB1()
        {
            string hola = "";
            company = new Company();


            company.Server = ConfigurationManager.AppSettings["B1Server"];
            company.CompanyDB = ConfigurationManager.AppSettings["B1CompanyDB"];
            company.DbServerType = BoDataServerTypes.dst_HANADB;
            company.DbUserName = ConfigurationManager.AppSettings["B1DbUserName"];
            company.DbPassword = ConfigurationManager.AppSettings["B1DbPassword"];
            company.UserName = ConfigurationManager.AppSettings["B1UserName"];
            company.Password = ConfigurationManager.AppSettings["B1Password"];
            company.language = BoSuppLangs.ln_English_Gb;
            company.UseTrusted = true;
            company.LicenseServer = ConfigurationManager.AppSettings["B1LicenseServer"];
            company.SLDServer = ConfigurationManager.AppSettings["B1SLDServer"];



            connectionResult = company.Connect();
            var x = company.Connected;
            if (connectionResult != 0)
            {
                company.GetLastError(out errorCode, out errorMessage);
                hola = company.GetLastErrorDescription();
            }

            string khe = hola;
            return connectionResult;
        }

        public bool TestHanaConection()
        {
            string cadenadeconexion = "Server=" + ConfigurationManager.AppSettings["B1Server"] +
                                      ";UserID=" + ConfigurationManager.AppSettings["HanaBDUser"] +
                                      ";Password=" + ConfigurationManager.AppSettings["HanaPassword"] +
                                      ";Current Schema=" + ConfigurationManager.AppSettings["HanaBD"];
            //Realizamos la conexion a SQL                            
            bool resultado = false;
            try
            {
                HanaConnection da = new HanaConnection(cadenadeconexion);
                da.Open();
                resultado = true;
                da.Close();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                resultado = false;
            }
            return resultado;
        }

        private B1SDKLog initLog(int userId, string type, string ObjectId)
        {
            B1SDKLog log = new B1SDKLog();
            log.Id = B1SDKLog.GetNextId(_context);
            log.UserId = userId;
            log.ObjectId = ObjectId;
            log.BusinessObject = type;
            log.Success = true;
            return log;
        }

        public string updatePersonToEmployeeMasterData(int UserId, People person, string position)
        {
            var log = initLog(UserId, BusinessObjectType.Employee, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();

                    SAPbobsCOM.EmployeesInfo oEmployeesInfo = (SAPbobsCOM.EmployeesInfo)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oEmployeesInfo);

                    oEmployeesInfo.GetByKey(person.SAPCodeRRHH.Value);
                    //oEmployeesInfo. = getEmpSAPId(person);
                    oEmployeesInfo.FirstName = person.Names;
                    oEmployeesInfo.LastName = person.FirstSurName;
                    oEmployeesInfo.Gender = person.Gender == "M" ? BoGenderTypes.gt_Male : BoGenderTypes.gt_Female;
                    oEmployeesInfo.DateOfBirth = person.BirthDate;
                    oEmployeesInfo.ExternalEmployeeNumber = person.CUNI;
                    oEmployeesInfo.IdNumber = person.Document;
                    //oEmployeesInfo.Department = Int32.Parse(person.GetLastContract().Dependency.Cod);
                    oEmployeesInfo.Active = BoYesNoEnum.tYES;
                    // set Job Title
                    oEmployeesInfo.JobTitle = position;

                    // set Branch Code

                    var brs = _context.Branch.ToList();
                    foreach (var b in brs)
                    {
                        oEmployeesInfo.EmployeeBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                        oEmployeesInfo.EmployeeBranchAssignment.Delete();
                    }

                    var bplid = Int32.Parse(person.GetLastContract().Branches.CodigoSAP);
                    oEmployeesInfo.EmployeeBranchAssignment.BPLID = bplid;
                    oEmployeesInfo.EmployeeBranchAssignment.Add();

                    var ou = person.GetLastContract();
                    oEmployeesInfo.UserFields.Fields.Item("U_UnidadOrg").Value = ou.Dependency.OrganizationalUnit.Cod;
                    oEmployeesInfo.UserFields.Fields.Item("U_Cod_SN").Value = "R" + person.CUNI;
                    if (person.SecondSurName != null)
                        oEmployeesInfo.UserFields.Fields.Item("U_ApMaterno").Value = person.SecondSurName;

                    oEmployeesInfo.Update();
                    string newKey = company.GetNewObjectKey();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                        }
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                            newKey = newKey.Replace("\t1", "");
                        }
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return newKey;
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                if (company.InTransaction)
                {
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
        }

        public string addPersonToEmployeeMasterData(int UserId, People person, string position)
        {
            var log = initLog(UserId, BusinessObjectType.Employee, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();
                    SAPbobsCOM.EmployeesInfo oEmployeesInfo = (SAPbobsCOM.EmployeesInfo)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oEmployeesInfo);

                    oEmployeesInfo.FirstName = person.Names;
                    oEmployeesInfo.LastName = person.FirstSurName;
                    oEmployeesInfo.Gender = person.Gender == "M" ? BoGenderTypes.gt_Male : BoGenderTypes.gt_Female;
                    oEmployeesInfo.DateOfBirth = person.BirthDate;
                    oEmployeesInfo.ExternalEmployeeNumber = person.CUNI;
                    oEmployeesInfo.IdNumber = person.Document;
                    //oEmployeesInfo.Department = Int32.Parse(person.GetLastContract().Dependency.Cod);
                    oEmployeesInfo.Active = BoYesNoEnum.tYES;
                    var ou = person.GetLastContract();
                    oEmployeesInfo.UserFields.Fields.Item("U_UnidadOrg").Value = ou.Dependency.OrganizationalUnit.Cod;
                    oEmployeesInfo.UserFields.Fields.Item("U_Cod_SN").Value = "R" + person.CUNI;
                    // set Job Title
                    oEmployeesInfo.JobTitle = position;
                    // set Branch Code
                    oEmployeesInfo.EmployeeBranchAssignment.BPLID = Int32.Parse(person.GetLastContract().Branches.CodigoSAP);
                    oEmployeesInfo.EmployeeBranchAssignment.Add();

                    oEmployeesInfo.Add();
                    string newKey = company.GetNewObjectKey();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                        }
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                            newKey = newKey.Replace("\t1", "");
                            person.SAPCodeRRHH = Int32.Parse(newKey);
                            _context.Person.AddOrUpdate(person);
                        }
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return newKey;
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                if (company.InTransaction)
                {
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                return "ERROR";
            }
        }

        /* -----------------------------------------------------------------------------------------------------------------------------------------------------------
           -----------------------------------------------------------------------------------------------------------------------------------------------------------
           ---------------------------------------------------SOCIO DE NEGOCIO CLIENTE (R-CUNI)-----------------------------------------------------------------------
           -----------------------------------------------------------------------------------------------------------------------------------------------------------
           ----------------------------------------------------------------------------------------------------------------------------------------------------------- */
        private string BPClientPrefix = "R";

        // -----------------------------------------------------------------------UPDATE ----------------------------------------------------------------------------
        public string updatePersonInBussinesPartner(int UserId, People person)
        {
            var log = initLog(UserId, BusinessObjectType.BussinesPartner, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();
                    SAPbobsCOM.BusinessPartners businessObject =
                        (SAPbobsCOM.BusinessPartners)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes
                            .oBusinessPartners);
                    //if person exist as BusinesPartner
                    if (businessObject.GetByKey(this.BPClientPrefix + person.CUNI))
                    {
                        businessObject.CardName = person.GetFullName();
                        businessObject.CardForeignName = person.GetFullName();
                        businessObject.CardType = SAPbobsCOM.BoCardTypes.cCustomer;
                        businessObject.CardCode = this.BPClientPrefix + person.CUNI;
                        string currency = businessObject.Currency;
                        businessObject.Currency = currency;

                        // set NIT
                        businessObject.UserFields.Fields.Item("LicTradNum").Value = person.Document;
                        // Set Group RRHH
                        businessObject.GroupCode = 108;
                        businessObject.UserFields.Fields.Item("GroupNum").Value = 6;
                        //add deb account
                        var contract = person.GetLastContract(_context);
                        businessObject.UserFields.Fields.Item("DebPayAcct").Value = this.getAccountId(contract.Branches.CuentaSociosRCUNI);

                        // set Branch Code
                        //var brs = _context.Branch.ToList();
                        //foreach (var b in brs)
                        //{
                        //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                        //    businessObject.BPBranchAssignment.Delete();
                        //}

                        //foreach (var b in brs)
                        //{
                        //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                        //    businessObject.BPBranchAssignment.Add();
                        //}

                        businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        var BR_COD_SAP = _context.Branch.FirstOrDefault(x => x.Id == contract.Dependency.BranchesId)
                            .CodigoSAP;
                        businessObject.BPBranchAssignment.BPLID = Int32.Parse(BR_COD_SAP);
                        businessObject.BPBranchAssignment.Add();

                        // save new business partner
                        businessObject.Update();
                        // get the new code
                        string newKey = company.GetNewObjectKey();
                        company.GetLastError(out errorCode, out errorMessage);
                        if (errorCode != 0)
                        {
                            log.Success = false;
                            log.ErrorCode = errorCode.ToString();
                            log.ErrorMessage = "SDK: " + errorMessage;
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                            }
                            return "ERROR";
                        }
                        else
                        {
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                                newKey = newKey.Replace("\t1", "");
                                _context.SdkErrorLogs.Add(log);
                                _context.SaveChanges();
                                return newKey;
                            }
                        }
                    }
                    log.Success = false;
                    log.ErrorMessage = "SDK: Not Found";
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                    return "ERROR";
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                if (company.InTransaction)
                {
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                return "ERROR";
            }
        }


        // ----------------------------------------------------------------------- ADD ----------------------------------------------------------------------------
        public string addpersonToBussinesPartner(int UserId, People person)
        {
            var log = initLog(UserId, BusinessObjectType.BussinesPartner, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();
                    SAPbobsCOM.BusinessPartners businessObject = (SAPbobsCOM.BusinessPartners)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oBusinessPartners);

                    businessObject.CardName = person.GetFullName();
                    businessObject.CardForeignName = person.GetFullName();
                    businessObject.CardType = SAPbobsCOM.BoCardTypes.cCustomer;
                    businessObject.CardCode = this.BPClientPrefix + person.CUNI;

                    // set NIT
                    businessObject.UserFields.Fields.Item("LicTradNum").Value = person.Document;
                    // Set Group RRHH
                    businessObject.GroupCode = 108;
                    businessObject.UserFields.Fields.Item("GroupNum").Value = 6;
                    //add deb account
                    var contract = person.GetLastContract(_context);
                    businessObject.UserFields.Fields.Item("DebPayAcct").Value = this.getAccountId(contract.Branches.CuentaSociosRCUNI);

                    // set Branch Code
                    //var brs = _context.Branch.ToList();
                    //foreach (var b in brs)
                    //{
                    //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                    //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                    //    businessObject.BPBranchAssignment.Delete();
                    //}
                    var BR_COD_SAP = _context.Branch.FirstOrDefault(x => x.Id == contract.Dependency.BranchesId)
                        .CodigoSAP;
                    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                    businessObject.BPBranchAssignment.BPLID = Int32.Parse(BR_COD_SAP);
                    businessObject.BPBranchAssignment.Add();

                    // save new business partner
                    businessObject.Add();
                    // get the new code
                    string newKey = company.GetNewObjectKey();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                        }
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                            newKey = newKey.Replace("\t1", "");
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            return newKey;
                        }
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                if (company.InTransaction)
                {
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                return "ERROR";
            }
        }

        /* -----------------------------------------------------------------------------------------------------------------------------------------------------------
           -----------------------------------------------------------------------------------------------------------------------------------------------------------
           ---------------------------------------------------SOCIO DE NEGOCIO SUPPLIER (H-CUNI)----------------------------------------------------------------------
           -----------------------------------------------------------------------------------------------------------------------------------------------------------
           ----------------------------------------------------------------------------------------------------------------------------------------------------------- */
        private string BPSupplierPrefix = "H";

        // ----------------------------------------------------------------------- UPDATE ----------------------------------------------------------------------------
        public string updatePersonInBussinesPartnerSUPPLIER(int UserId, People person)
        {
            var log = initLog(UserId, BusinessObjectType.BussinesPartner, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();
                    SAPbobsCOM.BusinessPartners businessObject =
                        (SAPbobsCOM.BusinessPartners)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes
                            .oBusinessPartners);
                    //if person exist as BusinesPartner
                    if (businessObject.GetByKey(this.BPSupplierPrefix + person.CUNI))
                    {
                        businessObject.CardName = person.GetFullName();
                        businessObject.CardForeignName = person.GetFullName();
                        businessObject.CardType = SAPbobsCOM.BoCardTypes.cSupplier;
                        businessObject.CardCode = this.BPSupplierPrefix + person.CUNI;
                        businessObject.LinkedBusinessPartner = this.BPClientPrefix + person.CUNI;
                        string currency = businessObject.Currency;
                        businessObject.Currency = currency;

                        // set NIT
                        businessObject.UserFields.Fields.Item("LicTradNum").Value = person.Document;
                        // Set Group RRHH
                        businessObject.GroupCode = 111;
                        businessObject.UserFields.Fields.Item("GroupNum").Value = 6;
                        //add deb account
                        var contract = person.GetLastContract(_context);
                        businessObject.UserFields.Fields.Item("DebPayAcct").Value = this.getAccountId(contract.Branches.CuentaSociosHCUNI);
                        //Indicador Impuesto
                        businessObject.UserFields.Fields.Item("VatGroup").Value = contract.Branches.VatGroup;

                        // set Branch Code
                        //var brs = _context.Branch.ToList();

                        //foreach (var b in brs)
                        //{
                        //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                        //    businessObject.BPBranchAssignment.Delete();
                        //}

                        //foreach (var b in brs)
                        //{
                        //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                        //    // If only one Branch
                        //    //businessObject.BPBranchAssignment.Delete();
                        //    businessObject.BPBranchAssignment.Add();
                        //}

                        // If only one Branch
                        businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                        businessObject.BPBranchAssignment.BPLID = Int32.Parse(contract.Branches.CodigoSAP);
                        businessObject.BPBranchAssignment.Add();


                        // save new business partner
                        businessObject.Update();
                        // get the new code
                        string newKey = company.GetNewObjectKey();
                        company.GetLastError(out errorCode, out errorMessage);
                        if (errorCode != 0)
                        {
                            log.Success = false;
                            log.ErrorCode = errorCode.ToString();
                            log.ErrorMessage = "SDK: " + errorMessage;
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                            }
                            return "ERROR";
                        }
                        else
                        {
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                                newKey = newKey.Replace("\t1", "");
                                _context.SdkErrorLogs.Add(log);
                                _context.SaveChanges();
                                return newKey;
                            }
                        }
                    }
                    log.Success = false;
                    log.ErrorMessage = "SDK: Not Found";
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                    return "ERROR";
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                if (company.InTransaction)
                {
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                }
                return "ERROR";
            }
        }


        // ----------------------------------------------------------------------- ADD ----------------------------------------------------------------------------
        public string addpersonToBussinesPartnerSUPPLIER(int UserId, People person)
        {
            var log = initLog(UserId, BusinessObjectType.BussinesPartner, person.Id.ToString());
            try
            {
                if (company.Connected)
                {
                    company.StartTransaction();
                    SAPbobsCOM.BusinessPartners businessObject = (SAPbobsCOM.BusinessPartners)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oBusinessPartners);

                    businessObject.CardName = person.GetFullName();
                    businessObject.CardForeignName = person.GetFullName();
                    businessObject.CardType = SAPbobsCOM.BoCardTypes.cSupplier;
                    businessObject.CardCode = this.BPSupplierPrefix + person.CUNI;
                    businessObject.LinkedBusinessPartner = this.BPClientPrefix + person.CUNI;

                    // set NIT
                    businessObject.UserFields.Fields.Item("LicTradNum").Value = person.Document;
                    // Set Group RRHH
                    businessObject.GroupCode = 111;
                    businessObject.UserFields.Fields.Item("GroupNum").Value = 6;
                    //add deb account
                    var contract = person.GetLastContract(_context);
                    businessObject.UserFields.Fields.Item("DebPayAcct").Value = this.getAccountId(contract.Branches.CuentaSociosHCUNI);
                    //Indicador Impuesto
                    businessObject.UserFields.Fields.Item("VatGroup").Value = contract.Branches.VatGroup;

                    // set Branch Code
                    //var brs = _context.Branch.ToList();
                    //foreach (var b in brs)
                    //{
                    //    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                    //    businessObject.BPBranchAssignment.BPLID = Int32.Parse(b.CodigoSAP);
                    //    businessObject.BPBranchAssignment.Add();
                    //}

                    businessObject.BPBranchAssignment.DisabledForBP = SAPbobsCOM.BoYesNoEnum.tNO;
                    businessObject.BPBranchAssignment.BPLID = Int32.Parse(contract.Branches.CodigoSAP);
                    businessObject.BPBranchAssignment.Add();

                    // save new business partner
                    businessObject.Add();
                    // get the new code
                    string newKey = company.GetNewObjectKey();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                            newKey = newKey.Replace("\t1", "");
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            return newKey;
                        }
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message + " At line: " + line;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
        }

        public string getAccountId(string accountCode)
        {
            string query = "select \"AcctCode\" from " + DatabaseName + ".oact where \"FormatCode\" = '" + accountCode + "'";
            var res = _context.Database.SqlQuery<string>(query).ToList()[0];
            return res;
        }

        public bool PersonExistsAsRRHH(People person)
        {
            string query = "select \"ExtEmpNo\" "
                           + "from " + DatabaseName + ".ohem"
                           + " WHERE \"ExtEmpNo\" = '" + person.CUNI + "'";
            var res = _context.Database.SqlQuery<string>(query);
            return res.Count() > 0;
        }

        // BP Exist as CLIENT
        public bool PersonExistsAsBusinessPartner(People person)
        {
            string query = "select \"CardCode\" "
                           + "from " + DatabaseName + ".ocrd"
                           + " WHERE \"CardCode\" = '" + this.BPClientPrefix + person.CUNI + "'";
            var res = _context.Database.SqlQuery<string>(query);
            return res.Count() > 0;
        }

        // BP Exist as SUPPLIER
        public bool PersonExistsAsBusinessPartnerSUPPLIER(People person)
        {
            string query = "select \"CardCode\" "
                           + "from " + DatabaseName + ".ocrd"
                           + " WHERE \"CardCode\" = '" + this.BPSupplierPrefix + person.CUNI + "'";
            var res = _context.Database.SqlQuery<string>(query);
            return res.Count() > 0;
        }

        public string AddOrUpdatePerson(int UserId, People person, string position, bool update = true)
        {
            string res = "";
            res = AddOrUpdatePersonToBusinessPartner(UserId, person, update: update);
            res += AddOrUpdatePersonToRRHH(UserId, person, position, update: update);
            res += AddOrUpdatePersonToBusinessPartnerSUPPLIER(UserId, person, update: update);
            return res;
        }

        public string AddOrUpdatePersonToRRHH(int UserId, People person, string position, bool update = true)
        {
            if (!PersonExistsAsRRHH(person))
            {
                return addPersonToEmployeeMasterData(UserId, person, position);
            }
            else if (update)
            {
                return updatePersonToEmployeeMasterData(UserId, person, position);
            }
            else return "Exist not created";
        }

        public string AddOrUpdatePersonToBusinessPartner(int UserId, People person, bool update = true)
        {
            if (!PersonExistsAsBusinessPartner(person))
            {
                return addpersonToBussinesPartner(UserId, person);
            }
            else if (update)
            {
                return updatePersonInBussinesPartner(UserId, person);
            }
            else return "Exist not created";
        }

        public string AddOrUpdatePersonToBusinessPartnerSUPPLIER(int UserId, People person, bool update = true)
        {
            if (!PersonExistsAsBusinessPartnerSUPPLIER(person))
            {
                return addpersonToBussinesPartnerSUPPLIER(UserId, person);
            }
            else if (update)
            {
                return updatePersonInBussinesPartnerSUPPLIER(UserId, person);
            }
            else return "Exist not created";
        }



        public string addVoucher(int UserId, Dist_Process process)
        {
            var log = initLog(UserId, BusinessObjectType.Voucher, process.Id.ToString());
            bool approved = true;
            try
            {
                string query = "SELECT \"ParentKey\",\"LineNum\",\"AccountCode\",sum(\"Debit\") \"Debit\",sum(\"Credit\") \"Credit\", \"ShortName\", null as \"LineMemo\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\" " +
                                                                                        " FROM (" +
                                                                                        " select x.\"Id\" \"ParentKey\"," +
                                                                                        "  null \"LineNum\"," +
                                                                                        "  coalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") \"AccountCode\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Debit\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Credit\"," +
                                                                                        "  x.\"BussinesPartner\" \"ShortName\"," +
                                                                                        "  x.\"Concept\" \"LineMemo\"," +
                                                                                        "  x.\"Project\" \"ProjectCode\"," +
                                                                                        "  f.\"Cod\" \"CostingCode\"," +
                                                                                        "  x.\"PEI\" \"CostingCode2\"," +
                                                                                        "  x.\"PlanEstudios\" \"CostingCode3\"," +
                                                                                        "  x.\"Paralelo\" \"CostingCode4\"," +
                                                                                        "  x.\"Periodo\" \"CostingCode5\"," +
                                                                                        "  x.\"CodigoSAP\" \"BPLId\"" +
                                                                                        " from  (SELECT a.\"Id\",  a.\"Document\",a.\"TipoEmpleado\",a.\"Dependency\",a.\"PEI\"," +
                                                                                        "           a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\"," +
                                                                                        "           a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\",a.\"BussinesPartner\"," +
                                                                                        "           b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\"" +
                                                                                        "           FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                                                                                        "               INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                                                                                        "               on a.\"DistProcessId\"=b.\"Id\" " +
                                                                                        "           AND a.\"DistProcessId\"= " + process.Id +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                                                                                        "                on a.\"TipoEmpleado\"=c.\"Name\" " +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
                                                                                        "              on c.\"GrupoContableId\" = d.\"GrupoContableId\"" +
                                                                                        "           and b.\"BranchesId\" = d.\"BranchesId\" " +
                                                                                        "           and a.\"Columna\" = d.\"Concept\" " +
                                                                                        "           INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
                                                                                        "              on b.\"BranchesId\" = e.\"Id\") x" +
                                                                                        " left join \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact b" +
                                                                                        " on x.CUENTASCONTABLES=b.\"FormatCode\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"Dependency\" d" +
                                                                                        " on x.\"Dependency\"=d.\"Cod\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"OrganizationalUnit\" f" +
                                                                                        " on d.\"OrganizationalUnitId\"=f.\"Id\"" +
                                                                                        ") V " +
                                                                                        "GROUP BY \"ParentKey\",\"LineNum\",\"AccountCode\", \"ShortName\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\";";
                IEnumerable<SapVoucher> dist = _context.Database.SqlQuery<SapVoucher>(query).ToList();
                var Auxdate = new DateTime(
                   Int32.Parse(process.gestion),
                   Int32.Parse(process.mes) > 12 ? (Int32.Parse(process.mes) - 12) : Int32.Parse(process.mes),
                   DateTime.DaysInMonth(Int32.Parse(process.gestion), Int32.Parse(process.mes) > 12 ? (Int32.Parse(process.mes) - 12) : Int32.Parse(process.mes))
               );
                var debe = dist.Sum(x => decimal.Parse(x.Debit));
                var haber = dist.Sum(x => decimal.Parse(x.Credit));
                if (debe != haber)
                {
                    // no cuadra debe y haber
                    log.Success = false;
                    log.ErrorCode = errorCode.ToString();
                    log.ErrorMessage = "System: Diferencia entre deba y haber. Debe(" + debe + ") - Haber(" + haber + ")";
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                    return "ERROR";
                }

                // If process Date is null set last day of the month in proccess
                DateTime date = process.RegisterDate == null ? Auxdate : process.RegisterDate.Value;

                if (company.Connected && dist.Count() > 0 && verifyAccounts(UserId, process.Id.ToString(), dist))
                {
                    company.StartTransaction();
                    var dist1 = dist.GroupBy(g => new
                    {
                        g.AccountCode,
                        g.ShortName,
                        g.CostingCode,
                        g.CostingCode2,
                        g.CostingCode3,
                        g.CostingCode4,
                        g.CostingCode5,
                        g.ProjectCode,
                        g.BPLId
                    })
                        .Select(g => new
                        {
                            g.Key.AccountCode,
                            g.Key.ShortName,
                            g.Key.CostingCode,
                            g.Key.CostingCode2,
                            g.Key.CostingCode3,
                            g.Key.CostingCode4,
                            g.Key.CostingCode5,
                            g.Key.ProjectCode,
                            g.Key.BPLId,
                            Credit = g.Sum(s => Double.Parse(s.Credit)),
                            Debit = g.Sum(s => Double.Parse(s.Debit))
                        }).OrderBy(z => z.Debit == 0.00d ? 1 : 0).ThenBy(z => z.AccountCode);

                    if (approved)
                    {
                        SAPbobsCOM.JournalEntries businessObject = (SAPbobsCOM.JournalEntries)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oJournalEntries);

                        // add header Journal Entrie Approved:
                        businessObject.ReferenceDate = date;
                        string strmes = "";
                        switch (process.mes)
                        {
                            case "13":
                                strmes = "RETROACTIVO1";
                                break;
                            case "14":
                                strmes = "RETROACTIVO2";
                                break;
                            case "15":
                                strmes = "RETROACTIVO3";
                                break;
                            case "16":
                                strmes = "RETROACTIVO4";
                                break;
                            default:
                                strmes = process.mes;
                                break;
                        }
                        string strMemo = "Planilla Sueldos y Salarios " + process.Branches.Abr + "-" + strmes + "-" + process.gestion;
                        businessObject.Memo = strMemo.Substring(0, strMemo.Length < 50 ? strMemo.Length : 49);
                        businessObject.TaxDate = date;
                        businessObject.Series = Int32.Parse(process.Branches.SerieComprobanteContalbeSAP);
                        businessObject.DueDate = date;


                        // add lines Journal Entrie Approved:
                        businessObject.Lines.SetCurrentLine(0);
                        foreach (var line in dist1)
                        {
                            businessObject.Lines.AccountCode = line.AccountCode;
                            businessObject.Lines.Credit = line.Credit;
                            businessObject.Lines.Debit = line.Debit;
                            if (line.ShortName != null)
                                businessObject.Lines.ShortName = line.ShortName;
                            businessObject.Lines.CostingCode = line.CostingCode;
                            businessObject.Lines.CostingCode2 = line.CostingCode2;
                            businessObject.Lines.CostingCode3 = line.CostingCode3;
                            businessObject.Lines.CostingCode4 = line.CostingCode4;
                            businessObject.Lines.CostingCode5 = line.CostingCode5;
                            businessObject.Lines.ProjectCode = line.ProjectCode;
                            businessObject.Lines.BPLID = Int32.Parse(line.BPLId);
                            businessObject.Lines.Add();
                        }

                        var B1key = businessObject.Add();

                        string newKey = company.GetNewObjectKey();
                        company.GetLastError(out errorCode, out errorMessage);
                        if (errorCode != 0)
                        {
                            if (company.InTransaction)
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                            log.Success = false;
                            log.ErrorCode = errorCode.ToString();
                            log.ErrorMessage = "SDK: " + errorMessage;
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            return "ERROR";
                        }
                        else
                        {
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                                newKey = newKey.Replace("\t1", "");
                                process.ComprobanteSAP = B1key.ToString();
                                _context.DistProcesses.AddOrUpdate(process);
                                _context.SdkErrorLogs.Add(log);
                                _context.SaveChanges();
                                return newKey;
                            }
                        }
                    }
                    else
                    {
                        SAPbobsCOM.JournalVouchers businessObject = (SAPbobsCOM.JournalVouchers)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oJournalVouchers);
                        // add header Vouvher:
                        businessObject.JournalEntries.ReferenceDate = date;
                        businessObject.JournalEntries.Memo = "Planilla prueba SDK SALOMON " + process.Branches.Abr;
                        businessObject.JournalEntries.TaxDate = date;
                        businessObject.JournalEntries.Series = Int32.Parse(process.Branches.SerieComprobanteContalbeSAP);
                        businessObject.JournalEntries.DueDate = date;

                        // add lines Voucher
                        businessObject.JournalEntries.Lines.SetCurrentLine(0);
                        foreach (var line in dist1)
                        {
                            businessObject.JournalEntries.Lines.AccountCode = line.AccountCode;
                            businessObject.JournalEntries.Lines.Credit = line.Credit;
                            businessObject.JournalEntries.Lines.Debit = line.Debit;
                            if (line.ShortName != null)
                                businessObject.JournalEntries.Lines.ShortName = line.ShortName;
                            if (line.CostingCode == null)
                                businessObject.JournalEntries.Lines.CostingCode = line.CostingCode;

                            businessObject.JournalEntries.Lines.CostingCode = line.CostingCode;
                            businessObject.JournalEntries.Lines.CostingCode2 = line.CostingCode2;
                            businessObject.JournalEntries.Lines.CostingCode3 = line.CostingCode3;
                            businessObject.JournalEntries.Lines.CostingCode4 = line.CostingCode4;
                            businessObject.JournalEntries.Lines.CostingCode5 = line.CostingCode5;
                            businessObject.JournalEntries.Lines.ProjectCode = line.ProjectCode;
                            businessObject.JournalEntries.Lines.BPLID = Int32.Parse(line.BPLId);
                            businessObject.JournalEntries.Lines.Add();
                        }

                        businessObject.Add();

                        string newKey = company.GetNewObjectKey();
                        company.GetLastError(out errorCode, out errorMessage);
                        if (errorCode != 0)
                        {
                            log.Success = false;
                            log.ErrorCode = errorCode.ToString();
                            log.ErrorMessage = "SDK: " + errorMessage;
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            return "ERROR";
                        }
                        else
                        {
                            if (company.InTransaction)
                            {
                                company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                                newKey = newKey.Replace("\t1", "");
                                _context.SdkErrorLogs.Add(log);
                                _context.SaveChanges();
                                return newKey;
                            }
                        }
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected or Voucher/Journal Entrie Data Error";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }

            catch (Exception ex)
            {
                log.Success = false;

                /*if (company.InTransaction)
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                */
                log.ErrorMessage = "Catch: " + ex.Message;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
        }

        private string CleanAndTrunk(string text, int size)
        {
            //remove special chars
            var goodText = Regex.Replace(text, "[^0-9A-Za-z ,-]", "");
            //remove new line characters
            goodText = Regex.Replace(goodText, @"\n|\r", "");
            return goodText.Substring(0, goodText.Length > size ? size : goodText.Length);
        }

        public string addServVoucher(int UserId, List<Serv_Voucher> voucher, ServProcess process)
        {
            var log = initLog(UserId, BusinessObjectType.Voucher, voucher.FirstOrDefault().Memo);
            bool approved = true;
            try
            {

                var debe = voucher.Sum(x => x.Debit);
                var haber = voucher.Sum(x => x.Credit);
                if (debe != haber)
                {
                    // no cuadra debe y haber
                    log.Success = false;
                    log.ErrorCode = errorCode.ToString();
                    log.ErrorMessage = "System: Diferencia entre deba y haber. Debe(" + debe + ") - Haber(" + haber + ")";
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                    return "ERROR";
                }

                // If process Date is null set last day of the month in proccess
                DateTime date = process.InSAPAt == null ? DateTime.Now : process.InSAPAt.Value;
                // date = new DateTime(2020, 9, 21);
                if (approved)
                {
                    JournalEntries businessObject =
                        (JournalEntries)company.GetBusinessObject(BoObjectTypes
                            .oJournalEntries);

                    // add header Journal Entrie Approved:
                    businessObject.ReferenceDate = date;

                    //businessObject.Memo = CleanAndTrunk(voucher.FirstOrDefault().Memo, 49); ya no se usará. Se conserva xceacaso
                    businessObject.Memo = voucher.FirstOrDefault().Memo;
                    businessObject.TaxDate = date;
                    businessObject.Series = Int32.Parse(process.Branches.SerieComprobanteContalbeSAP);
                    businessObject.DueDate = date;
                    businessObject.Reference2 = "SARAI LOTE N. " + process.Id;

                    // add lines Journal Entrie Approved:
                    businessObject.Lines.SetCurrentLine(0);
                    foreach (var line in voucher)
                    {
                        // var xx = CleanAndTrunk(line.LineMemo,49); comentado adrian
                        businessObject.Lines.LineMemo = line.LineMemo;
                        //businessObject.Lines.LineMemo = CleanAndTrunk(line.LineMemo, 49); ya no se usará. Se conserva xceacaso
                        businessObject.Lines.AccountCode = this.getAccountId(line.Account);
                        businessObject.Lines.Credit = (double)line.Credit;
                        businessObject.Lines.Debit = (double)line.Debit;
                        if (line.CardCode != null)
                            businessObject.Lines.ShortName = line.CardCode;
                        businessObject.Lines.CostingCode = line.OU;
                        businessObject.Lines.CostingCode2 = line.PEI;
                        businessObject.Lines.CostingCode3 = line.Carrera;
                        businessObject.Lines.CostingCode4 = line.Paralelo;
                        businessObject.Lines.CostingCode5 = line.Periodo;
                        businessObject.Lines.ProjectCode = line.ProjectCode;
                        businessObject.Lines.BPLID = Int32.Parse(process.Branches.CodigoSAP);
                        businessObject.Lines.Add();
                    }
                    businessObject.Add();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);

                        }
                        string newKey = company.GetNewObjectKey();


                        newKey = newKey.Replace("\t1", "");
                        process.SAPId = newKey;
                        _context.ServProcesses.AddOrUpdate(process);
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return newKey;
                    }
                }

                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected or Voucher/Journal Entrie Data Error";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.ErrorMessage = "Catch: " + ex.Message;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
        }

        public bool verifyAccounts(int UserId, string processId, IEnumerable<SapVoucher> lines)
        {
            foreach (var line in lines)
            {
                if (!checkVoucherAccount(UserId, processId, line, addErrorLog: true))
                    return false;
            }
            return true;
        }

        public bool checkVoucherAccount(int UserId, string processId, SapVoucher line, List<OACT> oact = null, bool addErrorLog = false)
        {
            bool res = true;
            OACT Uoact = null;
            if (oact == null)
            {
                Uoact = _context.Database.SqlQuery<OACT>("select \"AcctCode\",\"AcctName\",\"FormatCode\"," +
                                                             "\"Dim1Relvnt\",\"Dim2Relvnt\",\"Dim3Relvnt\",\"Dim4Relvnt\"," +
                                                             "\"Dim5Relvnt\",\"LocManTran\" from \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact" +
                                                             " where \"AcctCode\"='" + line.AccountCode + "';")
                    .FirstOrDefault();
            }
            else
            {
                Uoact = oact.FirstOrDefault(x => x.AcctCode == line.AccountCode);
            }

            if (Uoact == null)
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "AccountCode not Found in SAP: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim1
            if (Uoact.Dim1Relvnt == "Y" && line.CostingCode.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 1 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim2
            if (Uoact.Dim2Relvnt == "Y" && line.CostingCode2.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 2 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim3
            if (Uoact.Dim3Relvnt == "Y" && line.CostingCode3.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 3 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim4
            if (Uoact.Dim4Relvnt == "Y" && line.CostingCode4.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 4 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim5
            if (Uoact.Dim5Relvnt == "Y" && line.CostingCode5.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 5 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Associate Account
            if ((Uoact.LocManTran == "Y" && line.ShortName.IsNullOrWhiteSpace())
               || (Uoact.LocManTran == "N" && !line.ShortName.IsNullOrWhiteSpace()))
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Associate Account: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }

            return res;
        }

        public List<dynamic> getBusinessPartners(string col = "CardCode", CustomUser user = null, Branches branch = null)
        {
            List<dynamic> res = new List<dynamic>();
            string[] cols = new string[]
            {
                "\"CardCode\"", "\"CardName\"", "\"LicTradNum\"", "\"CardType\"", "\"GroupCode\"", "\"Series\""
                , "\"Currency\"", "\"City\"", "\"Country\""
            };

            string strcol = "";
            bool first = true;

            foreach (var column in cols)
            {
                strcol += (first ? "" : ",") + "a." + column;
                first = false;
            }

            if (connectedtoHana)
            {
                if (user != null)
                {
                    ADClass auth = new ADClass();
                    var branches = auth.getUserBranches(user);
                    string where = " where a.\"validFor\" = 'Y' and (";
                    int f = 0;
                    if (branches == null)
                        where += "false";
                    else
                        foreach (var br in branches)
                        {
                            if (f != 0)
                                where += " or ";
                            where += "b.\"BPLId\" = " + br.CodigoSAP;
                            f++;
                        }
                    where += " ) ";
                    string cl = col == "*" ? strcol : "a.\"" + col + "\"";
                    string query = "Select distinct " + cl + " from " + DatabaseName + ".OCRD a " +
                                   "inner join " + DatabaseName + ".CRD8 b " +
                                   " on a.\"CardCode\"=b.\"CardCode\"" + where + " order by a.\"CardCode\"";
                    HanaCommand command = new HanaCommand(query, HanaConn);
                    HanaDataReader dataReader = command.ExecuteReader();
                    if (dataReader.HasRows)
                    {
                        while (dataReader.Read())
                        {
                            if (col == "*")
                            {
                                dynamic x = new JObject();
                                foreach (var column in cols)
                                {
                                    x[column.Replace("\"", "")] = dataReader[column.Replace("\"", "")].ToString();
                                }
                                res.Add(x);
                            }
                            else
                                res.Add(dataReader[col].ToString());
                        }
                    }

                }
                else if (branch != null)
                {
                    ADClass auth = new ADClass();
                    string where = " where a.\"validFor\" = 'Y' and b.\"BPLId\" = " + branch.CodigoSAP;

                    string cl = col == "*" ? strcol : "a.\"" + col + "\"";

                    string query = "Select distinct " + cl + " from " + DatabaseName + ".OCRD a " +
                                   "inner join " + DatabaseName + ".CRD8 b " +
                                   " on a.\"CardCode\"=b.\"CardCode\"" + where;
                    HanaCommand command = new HanaCommand(query, HanaConn);
                    HanaDataReader dataReader = command.ExecuteReader();
                    if (dataReader.HasRows)
                    {
                        while (dataReader.Read())
                        {
                            if (col == "*")
                            {
                                dynamic x = new JObject();
                                foreach (var column in cols)
                                {
                                    x[column.Replace("\"", "")] = dataReader[column.Replace("\"", "")].ToString();
                                }
                                res.Add(x);
                            }
                            else
                                res.Add(dataReader[col].ToString());
                        }
                    }
                }
                else
                {
                    string cl = col == "*" ? col : "\"" + col + "\"";
                    string query = "Select " + cl + " from " + DatabaseName + ".OCRD  where \"validFor\" = 'Y' ";
                    HanaCommand command = new HanaCommand(query, HanaConn);
                    HanaDataReader dataReader = command.ExecuteReader();

                    if (dataReader.HasRows)
                    {
                        while (dataReader.Read())
                        {
                            if (col == "*")
                            {
                                dynamic x = new JObject();
                                foreach (var column in cols)
                                {
                                    x[column.Replace("\"", "")] = dataReader[column.Replace("\"", "")].ToString();
                                }
                                res.Add(x);
                            }
                            else
                                res.Add(dataReader[col].ToString());
                        }
                    }
                }
            }

            return res;
        }

        public List<dynamic> getProjects(string col = "PrjCode")
        {
            List<dynamic> res = new List<dynamic>();
            string[] dim1cols = new string[]
            {
                "\"PrjCode\"", "\"PrjName\"", "\"Locked\"", "\"DataSource\"", "\"ValidFrom\"",
                "\"ValidTo\"", "\"Active\"", "\"U_ModalidadProy\"", "\"U_Sucursal\"", "\"U_Tipo\"", "\"U_UORGANIZA\"", "\"U_PEI_PO\""
            };

            string strcol = "";
            bool first = true;

            foreach (var column in dim1cols)
            {
                strcol += (first ? "" : ",") + column;
                first = false;
            }

            if (connectedtoHana)
            {
                string query = "Select " + strcol + " from " + DatabaseName + ".OPRJ";
                HanaCommand command = new HanaCommand(query, HanaConn);
                HanaDataReader dataReader = command.ExecuteReader();

                if (dataReader.HasRows)
                {
                    while (dataReader.Read())
                    {
                        if (col == "*")
                        {
                            dynamic x = new JObject();
                            foreach (var column in dim1cols)
                            {
                                x[column.Replace("\"", "")] = dataReader[column.Replace("\"", "")].ToString();
                            }
                            res.Add(x);
                        }
                        else
                            res.Add(dataReader[col].ToString());
                    }
                }
            }

            return res;
        }

        //TO_VARCHAR (a."RefDate", 'DD/MM/YYYY')
        public List<dynamic> getCostCenter(Dimension dimesion, string mes = null, string gestion = null, string col = "PrcCode")
        {
            List<dynamic> res = new List<dynamic>();
            string validFrom =  "\"ValidFrom\"";
            string validTo =  "\"ValidTo\"";
            if (connectedtoHana)
            {
                string[][] dim1cols = new string[][]
            {
                new [] {"*"},
                new [] {"\"PrcCode\"", "\"PrcName\"", "\"ValidFrom\"", "\"ValidTo\"", "\"U_TipoUnidadO\""}, 
                new [] {"\"PrcCode\"", "\"PrcName\"", "\"ValidFrom\"", "\"ValidTo\"", "\"U_GestionCC\"", "\"U_AmbitoPEI\"", "\"U_DirectrizPEI\"", "\"U_Indicador\""},
                new [] {"\"PrcCode\"", "\"PrcName\"", "\"ValidFrom\"", "\"ValidTo\"", "\"U_NUM_INT_CAR\"", "\"U_Nivel\""},
                new [] {"\"PrcCode\"", "\"PrcName\"", "\"ValidFrom\"", "\"ValidTo\"", "\"U_PeriodoPARALELO\"", "\"U_Sigla\"", "\"U_Materia\"", "\"U_Paralelo\"", "\"U_ModalidadPARALELO\"", "\"U_EstadoParalelo\"", "\"U_NivelParalelo\"", "\"U_TipoParalelo\"", "\"U_Unidad_Organizacional\""},
                new [] {"\"PrcCode\"", "\"PrcName\"", "\"ValidFrom\"", "\"ValidTo\"", "\"U_GestionPeriodo\"", "\"U_TipoPeriodo\""},
            };

                string strcol = "";
                bool first = true;
                //arma los datos para el select según la dimensión
                foreach (var column in dim1cols[(int)dimesion])
                {
                    strcol += (first ? "" : ",") + column;
                    first = false;
                }
                /*si la dimension es 0, select todo-> revisar que el mes sea diferente de la gestion, si lo fuera-> busca proyectos que tengan el 1ro de enero entre sus fechas válidas
                 *  o que la primera fecha para la gestion y el mes sea mayor al VF y el VT es nulo
                 *  si el mes es igual a la gestión-> no hay where
                 si la dimensión no es 0 -> busca la dimension solicitada y los proyectos segun el mes y gestion entre fechas validas
                    el caso de VT nulo
                    si el mes es igual a la gestión->busca proyectos por dimension nada más*/

                //por qué el mes sería igual a la gestión? por si el mes y la gestion no se envían como parámetros -->llegan como null
                string where = (int)dimesion == 0
                    ? ((mes != gestion) ? " where ('2018-02-01 01:00:00' " +
                      "between \"ValidFrom\" and \"ValidTo\")" +
                      "or ('" + gestion + "-" + mes + "-01 01:00:00' > \"ValidFrom\" " +
                      "and \"ValidTo\" is null)" : "")
                    : ((mes != gestion) ? " where \"DimCode\"=" + (int)dimesion +
                      " and (('" + gestion + "-" + mes + "-01 01:00:00' " +
                      "between \"ValidFrom\" and \"ValidTo\")" +
                      "or (\"ValidFrom\" <='" + gestion + "-" + mes + "-01 01:00:00'" +
                      "and \"ValidTo\" is null))" : " where \"DimCode\"=" + (int)dimesion);
                string query = "Select " + strcol + " from " + DatabaseName + ".OPRC" + where;
                //ejecuta el query dinámico
                HanaCommand command = new HanaCommand(query, HanaConn);
                HanaDataReader dataReader = command.ExecuteReader();

                if (dataReader.HasRows)
                {
                    while (dataReader.Read())
                    {
                        if (col == "*")
                        {
                            dynamic x = new JObject();
                            foreach (var column in dim1cols[(int)dimesion])
                            {
                                x[column.Replace("\"", "")] = dataReader[column.Replace("\"", "")].ToString();
                            }
                            res.Add(x);
                        }
                        else
                            res.Add(dataReader[col].ToString());
                    }
                }
            }
            //devuelve lista dinámica con los elementos del query
            return res;
        }

        public List<object> getParalels()
        {
            List<dynamic> list = new List<dynamic>();

            if (connectedtoHana)
            {
                string query = "select a.\"PrcCode\", a.\"U_PeriodoPARALELO\", a.\"U_Sigla\", a.\"U_Paralelo\", b.\"CODUNIDADORGANIZACIONAL\", b.\"UNIDAD_AUXILIAR\", "
                + "b.\"SEDE\" as \"Segmento\"  "
                + "from ucatolica.oprc a "
                + "inner join admnal.\"T_REG_PARALELOS_NS\" b "
                + "on a.\"PrcCode\" = b.\"CODIGOSAP\" "
                + "WHERE a.\"DimCode\" = " + 4;

                HanaCommand command = new HanaCommand(query, HanaConn);
                HanaDataReader dataReader = command.ExecuteReader();

                if (dataReader.HasRows)
                {
                    while (dataReader.Read())
                    {
                        dynamic o = new JObject();
                        o.cod = dataReader["PrcCode"].ToString();
                        o.periodo = dataReader["U_PeriodoPARALELO"].ToString();
                        o.sigla = dataReader["U_Sigla"].ToString();
                        o.paralelo = dataReader["U_Paralelo"].ToString();
                        o.OU = dataReader["CODUNIDADORGANIZACIONAL"].ToString();
                        o.auxiliar = dataReader["UNIDAD_AUXILIAR"].ToString();
                        o.segmento = dataReader["Segmento"].ToString();
                        list.Add(o);
                    }
                }
            }

            return list;
        }

        public List<object> getCareers()
        {
            List<dynamic> list = new List<dynamic>();
            if (connectedtoHana)
            {
                string query = "select a.\"PrcCode\", a.\"PrcName\", a.\"ValidFrom\", a.\"ValidTo\", a.\"U_NUM_INT_CAR\", a.\"U_Nivel\", b.\"U_CODIGO_DEPARTAMENTO\", b.\"U_CODIGO_SEGMENTO\" "
                + "from ucatolica.oprc a "
                + "inner join ucatolica.\"@T_GEN_CARRERAS\" b "
                + " on a.\"PrcCode\" = b.\"U_CODIGO_CARRERA\" "
                + " WHERE a.\"DimCode\" = " + 3;

                HanaCommand command = new HanaCommand(query, HanaConn);
                HanaDataReader dataReader = command.ExecuteReader();

                if (dataReader.HasRows)
                {
                    while (dataReader.Read())
                    {
                        dynamic o = new JObject();
                        o.cod = dataReader["PrcCode"].ToString();
                        o.carrera = dataReader["PrcName"].ToString();
                        o.validFrom = dataReader["ValidFrom"].ToString();
                        o.validTo = dataReader["ValidTo"].ToString();
                        o.num_int_car = dataReader["U_NUM_INT_CAR"].ToString();
                        o.nivel = dataReader["U_Nivel"].ToString();
                        o.OU = dataReader["U_CODIGO_DEPARTAMENTO"].ToString();
                        o.segmento = dataReader["U_CODIGO_SEGMENTO"].ToString();
                        list.Add(o);
                    }
                }
            }

            return list;
        }
        
        public string getLastError()
        {
            return errorCode + ": " + errorMessage;
        }
        public string addPreliminar(int UserId, Dist_Process process, int RegOr, int processRegOr)
        {
            var log = initLog(UserId, BusinessObjectType.Voucher, process.Id.ToString());
            //string regionalOrigen = _context.Branch.Where(x => x.Id == RegOr).Select(x => x.Abr).FirstOrDefault();
            string seg = _context.Branch.Where(x => x.Id == RegOr).Select(x => x.Abr).FirstOrDefault();
            string th = "";
            bool approved = false;
            try
            {
                //" + CustomSchema.Schema + "
                //" + ConfigurationManager.AppSettings["B1CompanyDB"] + "
                string query = "(SELECT \"ParentKey\",\r\n\"LineNum\",\r\n\"AccountCode\", \r\nsum(\"Debit\") \"Debit\",\r\nsum(\"Credit\") \"Credit\", \r\n\"ShortName\", " +
                               "\r\n\"Fullname\" as \"LineMemo\",\r\n\"ProjectCode\",\"CostingCode\",\r\n\"CostingCode2\",\"CostingCode3\",\r\n\"CostingCode4\",\r\n\"CostingCode5\"," +
                               "\r\n\"BPLId\" " +
                               "\r\nFROM ( \r\nselect x.\"Id\" \"ParentKey\", x.\"TipoEmpleadoCuenta\", \r\nnull \"LineNum\",  \r\ncoalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") " +
                               "\"AccountCode\", \r\n  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Debit\"," +
                               "\r\n  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Credit\",\r\n  null \"ShortName\",\r\n  x.\"Concept\" \"LineMemo\", " +
                               "\r\n   x.\"Project\" \"ProjectCode\",  \r\n   f.\"Cod\" \"CostingCode\", \r\n    x.\"PEI\" \"CostingCode2\",\r\n     x.\"Fullname\"," +
                               "\r\n  x.\"PlanEstudios\" \"CostingCode3\",\r\n  x.\"Paralelo\" \"CostingCode4\", \r\n   x.\"Periodo\" \"CostingCode5\", \r\nx.\"CodigoSAP\" \"BPLId\"" +
                               "\r\n from  (SELECT a.\"Id\",  a.\"Document\",a.\"TipoEmpleado\",o.\"Dependency\",o.\"PEI\",concat(concat(o.\"Names\", ' '),o.\"SecondSurName\") as \"Fullname\", " +
                               " op.\"TipoEmpleadoCuenta\",\r\n o.\"PlanEstudios\",o.\"Paralelo\",o.\"Periodo\",o.\"Project\",\r\n a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\"," +
                               "\r\nb.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\"" +
                               "\r\n FROM " + CustomSchema.Schema + ".\"Dist_Cost\" a " +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_OR\" o on o.\"Id\" = a.\"DistORId\"" +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_Process\" b  on a.\"DistProcessId\"=b.\"Id\"  AND a.\"DistProcessId\"=   " + processRegOr +
                               "\r\n inner join " +
                               "  (select ord.\"Document\", ord.\"Id\", " +
                               "\r\n case when \"Paralelo\" != '' then 'TH' " +
                               "\r\nwhen \"PlanEstudios\" != '' then 'TH' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'E' then 'EC' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'F' then 'FC' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'P' then 'POST' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'S' then 'SA' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'V' then 'INV' " +
                               "\r\n else 'RP' end as \"TipoEmpleadoCuenta\" " +
                               "\r\n from " + CustomSchema.Schema + ".\"Dist_OR\" ord" +
                               "\r\n left join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj on oprj.\"PrjCode\" = ord.\"Project\"" +
                               "\r\n where ord.\"DistFileId\" in (SELECT a.\"Id\" from " + CustomSchema.Schema + ".\"Dist_File\" a" +
                               "\r\nINNER JOIN " + CustomSchema.Schema + ".\"Dist_Process\" b ON a.\"DistProcessId\"=b.\"Id\" where b.\"Id\"= " + +processRegOr +
                               "\r\n and a.\"State\" = 'UPLOADED')) op  " +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_TipoEmpleado\" c  on c.\"Name\" = op.\"TipoEmpleadoCuenta\"" +
                               "\r\non op.\"Document\" = o.\"Document\"\r\nand op.\"Id\" = a.\"DistORId\"" +
                               "\r\n INNER JOIN " + CustomSchema.Schema + ".\"Branches\" e  on o.\"segmento\" = e.\"Abr\"" +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"CuentasContables\" d  on c.\"GrupoContableId\" = d.\"GrupoContableId\" and e.\"Id\" = d.\"BranchesId\"  and a.\"Columna\" = d.\"Concept\"" +
                               "\r\nwhere d.\"Indicator\" = 'D' and\r\no.\"segmentoOrigen\"=  " + RegOr +
                               " and o.\"segmento\" = '" + process.Branches.Abr + "'\r\n) x" +
                               "\r\n left join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oact b on x.CUENTASCONTABLES=b.\"FormatCode\"" +
                               "\r\n left join " + CustomSchema.Schema + ".\"Dependency\" d on x.\"Dependency\"=d.\"Cod\"" +
                               "\r\n left join " + CustomSchema.Schema + ".\"OrganizationalUnit\" f on d.\"OrganizationalUnitId\"=f.\"Id\" ) V " +
                               "\r\n GROUP BY \"Fullname\", \"ParentKey\",\"LineNum\",\"AccountCode\", \"ShortName\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\", \"TipoEmpleadoCuenta\" order by \"Fullname\"\r\n)" +
                               " union all " + 
                               " (\r\n SELECT null \"ParentKey\",\r\nnull \"LineNum\",\r\n\"AccountCode\", \r\nsum(\"Debit\") \"Debit\",\r\nsum(\"Credit\") \"Credit\", " +
                               "\r\n\"ShortName\", \r\nnull \"LineMemo\",\r\n'' \"ProjectCode\",\r\n'' \"CostingCode\",\r\n'' \"CostingCode2\",\r\n'' \"CostingCode3\",\r\n'' \"CostingCode4\"," +
                               "\r\n'' \"CostingCode5\",\r\n\"BPLId\" \r\nFROM ( \r\nselect x.\"Id\" \"ParentKey\", x.\"TipoEmpleadoCuenta\", cc.\"Name\" \"Cuenta\",\r\nnull \"LineNum\",  " +
                               "\r\ncoalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") \"AccountCode\", \r\n  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Credit\"," +
                               "\r\n  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Debit\",\r\n  x.\"ShortName\",\r\n  x.\"Concept\" \"LineMemo\", " +
                               "\r\n   x.\"Project\" \"ProjectCode\",  \r\n   f.\"Cod\" \"CostingCode\", \r\n    x.\"PEI\" \"CostingCode2\",\r\n  x.\"PlanEstudios\" \"CostingCode3\"," +
                               "\r\n  x.\"Paralelo\" \"CostingCode4\", \r\n   x.\"Periodo\" \"CostingCode5\", x.\"BId\",\r\nx.\"CodigoSAP\" \"BPLId\"" +
                               "\r\n from  (SELECT a.\"Id\",  a.\"Document\",a.\"TipoEmpleado\",o.\"Dependency\",o.\"PEI\",concat(concat(o.\"Names\", ' '),o.\"SecondSurName\") as \"Fullname\", " +
                               " op.\"TipoEmpleadoCuenta\",\r\n o.\"PlanEstudios\",o.\"Paralelo\",o.\"Periodo\",o.\"Project\",\r\n a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\"," +
                               " concat(e.\"InterAcreedora\",bra.\"Abr\") \"ShortName\",\r\nb.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\", e.\"Id\" \"BId\"" +
                               "\r\n FROM " + CustomSchema.Schema + ".\"Dist_Cost\" a " +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_OR\" o on o.\"Id\" = a.\"DistORId\"" +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_Process\" b  on a.\"DistProcessId\"=b.\"Id\"  AND a.\"DistProcessId\"=   " + processRegOr +
                               "\r\n inner join " +
                               "  (select ord.\"Document\", ord.\"Id\", " +
                               "\r\n case when \"Paralelo\" != '' then 'TH' " +
                               "\r\nwhen \"PlanEstudios\" != '' then 'TH' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'E' then 'EC' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'F' then 'FC' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'P' then 'POST' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'S' then 'SA' " +
                               "\r\nwhen \"Project\" != '' and oprj.\"U_Tipo\" = 'V' then 'INV' " +
                               "\r\n else 'RP'end as \"TipoEmpleadoCuenta\" " +
                               "\r\n from " + CustomSchema.Schema + ".\"Dist_OR\" ord" +
                               "\r\n left join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj on oprj.\"PrjCode\" = ord.\"Project\"" +
                               "\r\n where ord.\"DistFileId\" in (SELECT a.\"Id\" from " + CustomSchema.Schema + ".\"Dist_File\" a" +
                               "\r\nINNER JOIN " + CustomSchema.Schema + ".\"Dist_Process\" b ON a.\"DistProcessId\"=b.\"Id\" where b.\"Id\"= " + +processRegOr +
                               "\r\n and a.\"State\" = 'UPLOADED')) op  " +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"Dist_TipoEmpleado\" c  on c.\"Name\" = op.\"TipoEmpleadoCuenta\"\r\non op.\"Document\" = o.\"Document\"\r\nand op.\"Id\" = a.\"DistORId\"" +
                               "\r\n INNER JOIN " + CustomSchema.Schema + ".\"Branches\" e  on o.\"segmento\" = e.\"Abr\"" +
                               "\r\n INNER JOIN  " + CustomSchema.Schema + ".\"CuentasContables\" d  on c.\"GrupoContableId\" = d.\"GrupoContableId\" and e.\"Id\" = d.\"BranchesId\"  and a.\"Columna\" = d.\"Concept\"" +
                               "\r\n inner join " + CustomSchema.Schema + ".\"Branches\" bra on bra.\"Id\" = o.\"segmentoOrigen\"" +
                               "\r\nwhere d.\"Indicator\" = 'D' and\r\no.\"segmentoOrigen\"=  " + RegOr +
                               " and o.\"segmento\" = '" + process.Branches.Abr + "'\r\n) x" +
                               "\r\n left join " + CustomSchema.Schema + ".\"Dependency\" d on x.\"Dependency\"=d.\"Cod\"" +
                               "\r\n left join " + CustomSchema.Schema + ".\"OrganizationalUnit\" f on d.\"OrganizationalUnitId\"=f.\"Id\" " +
                               "\r\n inner join " + CustomSchema.Schema + ".\"CuentasContables\" cc on cc.\"GrupoContableId\" = 28 and cc.\"BranchesId\" = x.\"BId\"" +
                               "\r\n left join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oact b on cc.\"Name\"=b.\"FormatCode\"\r\n ) V " +
                               "\r\n GROUP BY \"Cuenta\", \"ShortName\",\"BPLId\", \"AccountCode\"\r\n )";
                  
                IEnumerable<SapVoucher> dist = _context.Database.SqlQuery<SapVoucher>(query).ToList();
                var Auxdate = new DateTime(
                   Int32.Parse(process.gestion),
                   Int32.Parse(process.mes) > 12 ? (Int32.Parse(process.mes) - 12) : Int32.Parse(process.mes),
                   DateTime.DaysInMonth(Int32.Parse(process.gestion), Int32.Parse(process.mes) > 12 ? (Int32.Parse(process.mes) - 12) : Int32.Parse(process.mes))
               );
                /*
                var debe = dist.Sum(x => decimal.Parse(x.Debit));
                var haber = dist.Sum(x => decimal.Parse(x.Credit));
                if (debe != haber)
                {
                    // no cuadra debe y haber
                    log.Success = false;
                    log.ErrorCode = errorCode.ToString();
                    log.ErrorMessage = "System: Diferencia entre deba y haber. Debe(" + debe + ") - Haber(" + haber + ")";
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                    return "ERROR";
                }
                 * */
                // If process Date is null set last day of the month in proccess
                //DateTime date = process.RegisterDate == null ? Auxdate : process.RegisterDate.Value;
                DateTime date = Auxdate;

                if (company.Connected && dist.Count() > 0 && verifyAccountsPreliminar(UserId, process.Id.ToString(), dist))
                {
                    company.StartTransaction();
                    /*
                    var dist1 = dist
                        .GroupBy(g => new
                    {
                        g.AccountCode,
                        g.ShortName,
                        g.CostingCode,
                        g.CostingCode2,
                        g.CostingCode3,
                        g.CostingCode4,
                        g.CostingCode5,
                        g.ProjectCode,
                        g.LineMemo,
                        g.BPLId
                    })
                        .Select(g => new
                        {
                            g.Key.AccountCode,
                            g.Key.ShortName,
                            g.Key.CostingCode,
                            g.Key.CostingCode2,
                            g.Key.CostingCode3,
                            g.Key.CostingCode4,
                            g.Key.CostingCode5,
                            g.Key.ProjectCode,
                            g.Key.LineMemo,
                            g.Key.BPLId,
                            Credit = g.Sum(s => Double.Parse(s.Credit)),
                            Debit = g.Sum(s => Double.Parse(s.Debit))
                        }).OrderBy(z => z.Debit == 0.00d ? 1 : 0).ThenBy(z => z.LineMemo);

                    */
                    var dist1 = dist
                        .Select(g => new
                        {
                            g.AccountCode,
                            g.ShortName,
                            g.CostingCode,
                            g.CostingCode2,
                            g.CostingCode3,
                            g.CostingCode4,
                            g.CostingCode5,
                            g.ProjectCode,
                            g.LineMemo,
                            g.BPLId,
                            Debit = Double.Parse(g.Debit),
                            Credit = Double.Parse(g.Credit)
                        }).OrderBy(z => z.Debit == 0.00d ? 1 : 0).ThenBy(z => z.LineMemo).ThenBy(z => z.AccountCode);

                    SAPbobsCOM.JournalVouchers businessObject = (SAPbobsCOM.JournalVouchers)company.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oJournalVouchers);
                    // add header Vouvher:
                    businessObject.JournalEntries.ReferenceDate = date;
                    businessObject.JournalEntries.Memo = "Planilla Interregional Salomon " + seg + "-" + process.Branches.Abr;
                    businessObject.JournalEntries.TaxDate = date;
                    businessObject.JournalEntries.Series = Int32.Parse(process.Branches.SerieComprobanteContalbeSAP);
                    businessObject.JournalEntries.DueDate = date;

                    // add lines Voucher
                    businessObject.JournalEntries.Lines.SetCurrentLine(0);
                    foreach (var line in dist1)
                    {
                        businessObject.JournalEntries.Lines.AccountCode = line.AccountCode;
                        businessObject.JournalEntries.Lines.Credit = line.Credit;
                        businessObject.JournalEntries.Lines.Debit = line.Debit;
                        if (line.ShortName != null)
                            businessObject.JournalEntries.Lines.ShortName = line.ShortName;

                        if (line.CostingCode == null)
                            businessObject.JournalEntries.Lines.CostingCode = line.CostingCode;

                        businessObject.JournalEntries.Lines.CostingCode = line.CostingCode;
                        businessObject.JournalEntries.Lines.CostingCode2 = line.CostingCode2;
                        businessObject.JournalEntries.Lines.CostingCode3 = line.CostingCode3;
                        businessObject.JournalEntries.Lines.CostingCode4 = line.CostingCode4;
                        businessObject.JournalEntries.Lines.CostingCode5 = line.CostingCode5;
                        businessObject.JournalEntries.Lines.ProjectCode = line.ProjectCode;
                        if (line.LineMemo != null)
                        {
                            businessObject.JournalEntries.Lines.LineMemo = line.LineMemo;
                        }
                        else
                        {

                            businessObject.JournalEntries.Lines.LineMemo = "Planilla Interregional Salomon " + seg + "-" + process.Branches.Abr;
                        }
                        businessObject.JournalEntries.Lines.BPLID = Int32.Parse(line.BPLId);
                        businessObject.JournalEntries.Lines.Add();
                    }

                    businessObject.Add();

                    string newKey = company.GetNewObjectKey();
                    company.GetLastError(out errorCode, out errorMessage);
                    if (errorCode != 0)
                    {
                        log.Success = false;
                        log.ErrorCode = errorCode.ToString();
                        log.ErrorMessage = "SDK: " + errorMessage;
                        _context.SdkErrorLogs.Add(log);
                        _context.SaveChanges();
                        return "ERROR";
                    }
                    else
                    {
                        if (company.InTransaction)
                        {
                            company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_Commit);
                            newKey = newKey.Replace("\t1", "");
                            _context.SdkErrorLogs.Add(log);
                            _context.SaveChanges();
                            return newKey;
                        }
                    }
                }
                log.Success = false;
                log.ErrorMessage = "SDK: Not Connected or Voucher/Journal Entrie Data Error";
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }

            catch (Exception ex)
            {
                log.Success = false;

                /*if (company.InTransaction)
                    company.EndTransaction(SAPbobsCOM.BoWfTransOpt.wf_RollBack);
                */
                log.ErrorMessage = "Catch: " + ex.Message;
                _context.SdkErrorLogs.Add(log);
                _context.SaveChanges();
                return "ERROR";
            }
        }
        public bool verifyAccountsPreliminar(int UserId, string processId, IEnumerable<SapVoucher> lines)
        {
            foreach (var line in lines)
            {
                if (!checkVoucherAccountPreliminar(UserId, processId, line, addErrorLog: true))
                    return false;
            }
            return true;
        }

        public bool checkVoucherAccountPreliminar(int UserId, string processId, SapVoucher line, List<OACT> oact = null, bool addErrorLog = false)
        {
            bool res = true;
            OACT Uoact = null;
            if (oact == null)
            {
                Uoact = _context.Database.SqlQuery<OACT>("select \"AcctCode\",\"AcctName\",\"FormatCode\"," +
                                                             "\"Dim1Relvnt\",\"Dim2Relvnt\",\"Dim3Relvnt\",\"Dim4Relvnt\"," +
                                                             "\"Dim5Relvnt\",\"LocManTran\" from \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact" +
                                                             " where \"AcctCode\"='" + line.AccountCode + "';")
                    .FirstOrDefault();
            }
            else
            {
                Uoact = oact.FirstOrDefault(x => x.AcctCode == line.AccountCode);
            }

            if (Uoact == null)
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "AccountCode not Found in SAP: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim1
            if (Uoact.Dim1Relvnt == "Y" && line.CostingCode.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 1 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim2
            if (Uoact.Dim2Relvnt == "Y" && line.CostingCode2.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 2 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim3
            if (Uoact.Dim3Relvnt == "Y" && line.CostingCode3.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 3 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim4
            if (Uoact.Dim4Relvnt == "Y" && line.CostingCode4.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 4 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Dim5
            if (Uoact.Dim5Relvnt == "Y" && line.CostingCode5.IsNullOrWhiteSpace())
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Dim 5 Required: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            //check Associate Account
            /*
            if ((Uoact.LocManTran == "Y" && line.ShortName.IsNullOrWhiteSpace())
               || (Uoact.LocManTran == "N" && !line.ShortName.IsNullOrWhiteSpace()))
            {
                res = false;
                if (addErrorLog)
                {
                    var log = initLog(UserId, BusinessObjectType.Voucher, processId);
                    log.Success = false;
                    log.ErrorMessage = "Associate Account: Dist_Cost Id ->" + line.ParentKey;
                    _context.SdkErrorLogs.Add(log);
                    _context.SaveChanges();
                }
            }
            */
            return res;
        }
    }
}