using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("Access")]
    public class Access
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public string Description { get; set; }
        public bool Public { get; set; }
        public bool NedAuth { get; set; }

        public Resource Resource { get; set; }
        public int ResourceId { get; set; }


        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \""+CustomSchema.Schema+"\".\"rrhh_Access_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}