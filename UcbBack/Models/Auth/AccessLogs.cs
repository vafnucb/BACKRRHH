using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("AccessLogs")]
    public class AccessLogs
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public int? UserId { get; set; }
        public int? AccessId { get; set; }
        public bool Success { get; set; }
        public string ResponseCode { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_AccessLogs_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}