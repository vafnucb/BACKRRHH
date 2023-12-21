using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;

namespace UcbBack.Models.Serv
{
    [CustomSchema("Serv_Process")]
    public class ServProcess
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        public int BranchesId { get; set; }
        public Branches Branches { get; set; }
        public string FileType { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public int LastUpdatedBy { get; set; }
        public DateTime? InSAPAt { get; set; }
        public string State { get; set; }
        public string SAPId { get; set; }

        public struct Serv_FileState
        {
            public const string Started = "INICIADO";
            public const string PendingApproval = "ESPERANDO APROBACION";
            public const string INSAP = "IN SAP";
            public const string ERROR = "ERROR";
            public const string Rejected = "RECHAZADO";
            public const string Canceled = "ANULADO";
        }

        public struct Serv_FileType
        {
            public const string Varios = "VARIOS";
            public const string Proyectos = "PROYECTOS";
            public const string Carrera = "CARRERA";
            public const string Paralelo = "PARALELO";
        }

        public int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Serv_Process_sqs\".nextval FROM DUMMY;").ToList()[0];
        }

        public IEnumerable<Serv_Voucher> getVoucherData(ApplicationDbContext _context)
        {
            string query = null;
            switch (this.FileType)
            {
                case ServProcess.Serv_FileType.Varios:
                    query =
                        "select sv.\"CardCode\",sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ContractObjective\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\",  " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"TotalAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"TotalAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Varios\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'PPAGAR\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", ou.\"Cod\" as \"OU\",sv.\"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"ContractAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"ContractAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Varios\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'CONTRATO\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\",sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IT\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IT\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Varios\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IT\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\",sv.\"CardName\",null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IUE\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IUE\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Varios\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IUE\' " +
                        " and \"Serv_ProcessId\" = " + this.Id;
                    break;
                case ServProcess.Serv_FileType.Carrera:
                    query =
                        "select sv.\"CardCode\",sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"AssignedJob\"||\' \'||sv.\"Carrera\"||\' \'||sv.\"Student\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"TotalAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"TotalAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Carrera\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'PPAGAR\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", ou.\"Cod\" as \"OU\",sv.\"PEI\",null as \"Paralelo\",sv.\"Carrera\" as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"ContractAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"ContractAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Carrera\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'CONTRATO\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IT\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IT\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Carrera\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IT\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IUE\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IUE\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Carrera\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IUE\' " +
                        " and \"Serv_ProcessId\" = " + this.Id;
                    break;
                case ServProcess.Serv_FileType.Proyectos:
                    query =
                        "select sv.\"CardCode\",sv.\"CardName\", null as \"OU\",null \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"ProjectCode\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"AssignedJob\" || \' \' || sv.\"ProjectSAPName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"TotalAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"TotalAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Proyectos\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'PPAGAR\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", ou.\"Cod\" as \"OU\",sv.\"PEI\",null as \"Paralelo\",null as \"Carrera\",sv.\"Periodo\" as \"Periodo\",sv.\"ProjectSAPCode\" as \"ProjectCode\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"ContractAmount\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"ContractAmount\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Proyectos\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'CONTRATO\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"ProjectCode\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IT\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IT\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Proyectos\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IT\' " +
                        " and \"Serv_ProcessId\" = " + this.Id +
                        "  " +
                        " union all " +
                        " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"ProjectCode\",  " +
                        " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                        " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IUE\" else 0 end as \"Debit\", " +
                        " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IUE\"else 0 end as \"Credit\" " +
                        " from "+CustomSchema.Schema+".\"Serv_Proyectos\" sv " +
                        " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                        " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                        " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                        " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                        " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                        " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                        " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where gc.\"Id\">11 " +
                        " and \"Concept\" = \'IUE\' " +
                        " and \"Serv_ProcessId\" = " + this.Id;

                    break;
                case ServProcess.Serv_FileType.Paralelo:
                    query =
                        "select sv.\"CardCode\",sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                    " sv.\"ServiceName\" as \"Memo\", sv.\"Sigla\" || \' \' || sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                    " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"TotalAmount\" else 0 end as \"Debit\", " +
                    " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"TotalAmount\"else 0 end as \"Credit\" " +
                    " from "+CustomSchema.Schema+".\"Serv_Paralelo\" sv " +
                    " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                    " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                    " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                    " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                    " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                    " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                    " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                    " on sv.\"DependencyId\" = d.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                    " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                    " where gc.\"Id\">11 " +
                    " and \"Concept\" = \'PPAGAR\' " +
                    " and \"Serv_ProcessId\" = " + this.Id +
                    "  " +
                    " union all " +
                    " select null as \"CardCode\", sv.\"CardName\", ou.\"Cod\" as \"OU\",sv.\"PEI\",sv.\"ParalelSAP\" as \"Paralelo\",null as \"Carrera\",sv.\"Periodo\" as \"Periodo\",null as \"Proyecto\",  " +
                    " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                    " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"ContractAmount\" else 0 end as \"Debit\", " +
                    " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"ContractAmount\"else 0 end as \"Credit\" " +
                    " from "+CustomSchema.Schema+".\"Serv_Paralelo\" sv " +
                    " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                    " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                    " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                    " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                    " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                    " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                    " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                    " on sv.\"DependencyId\" = d.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                    " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                    " where gc.\"Id\">11 " +
                    " and \"Concept\" = \'CONTRATO\' " +
                    " and \"Serv_ProcessId\" = " + this.Id +
                    "  " +
                    " union all " +
                    " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                    " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                    " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IT\" else 0 end as \"Debit\", " +
                    " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IT\"else 0 end as \"Credit\" " +
                    " from "+CustomSchema.Schema+".\"Serv_Paralelo\" sv " +
                    " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                    " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                    " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                    " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                    " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                    " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                    " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                    " on sv.\"DependencyId\" = d.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                    " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                    " where gc.\"Id\">11 " +
                    " and \"Concept\" = \'IT\' " +
                    " and \"Serv_ProcessId\" = " + this.Id +
                    "  " +
                    " union all " +
                    " select null as \"CardCode\", sv.\"CardName\", null as \"OU\",null as \"PEI\",null as \"Paralelo\",null as \"Carrera\",null as \"Periodo\",null as \"Proyecto\",  " +
                    " sv.\"ServiceName\" as \"Memo\", sv.\"ServiceName\" as \"LineMemo\",sv.\"AssignedAccount\",\"Concept\",cc.\"Name\" as \"Account\", " +
                    " CASE WHEN cc.\"Indicator\"=\'D\' then sv.\"IUE\" else 0 end as \"Debit\", " +
                    " CASE WHEN cc.\"Indicator\"=\'H\' then sv.\"IUE\"else 0 end as \"Credit\" " +
                    " from "+CustomSchema.Schema+".\"Serv_Paralelo\" sv " +
                    " inner join "+CustomSchema.Schema+".\"GrupoContable\" gc " +
                    " on sv.\"AssignedAccount\"= gc.\"Name\" " +
                    " inner join "+CustomSchema.Schema+".\"CuentasContables\" cc " +
                    " on cc.\"GrupoContableId\" = gc.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"Serv_Process\" sp " +
                    " on sv.\"Serv_ProcessId\" = sp.\"Id\" " +
                    " and cc.\"BranchesId\" = sp.\"BranchesId\" " +
                    " inner join "+CustomSchema.Schema+".\"Dependency\" d " +
                    " on sv.\"DependencyId\" = d.\"Id\" " +
                    " inner join "+CustomSchema.Schema+".\"OrganizationalUnit\" ou " +
                    " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                    " where gc.\"Id\">11 " +
                    " and \"Concept\" = \'IUE\' " +
                    " and \"Serv_ProcessId\" = " + this.Id;
                    break;
            }

            if (query == null)
                return null;

            IEnumerable<Serv_Voucher> voucher = _context.Database.SqlQuery<Serv_Voucher>(query).ToList();
            return voucher;
        }
    }
}