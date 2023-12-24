using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("TipoPago")]
    public class TipoPago
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Nombre { get; set; }
        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_TipoPago_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}