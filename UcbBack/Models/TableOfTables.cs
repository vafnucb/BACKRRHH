using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("TableOfTables")]
    public class TableOfTables
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }
        public string Type { get; set; }
        public String Value { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_TableOfTables_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
        
    }
    public struct TableOfTablesTypes
    {
        public static string Linkage = "VINCULACION";
        public static string Dedication = "DEDICACION";
        public static string CauseDesvinculation = "CAUSA DESVINCULACION";
        public static string AFP = "AFP";
        public static string ProcessState = "PROCESS STATE";
        public static string FileState = "FILE STATE";
        public static string Serv_FileState = "SERV FILE STATE";
        public static string Serv_FileType = "SERV FILE TYPE";
    }
}