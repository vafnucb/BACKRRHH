using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("RolhasAccess")]
    public class RolhasAccess
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int Accessid { get; set; }
        public Access Access { get; set; }
        public int Rolid { get; set; }
        public Rol Rol { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_RolhasAccess_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}