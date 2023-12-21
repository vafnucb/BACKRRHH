using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("TempAlta")]
    public class TempAlta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        public string CUNI { get; set; }
        //public string TypeDocument { get; set; }
        public string Document { get; set; }
        //public string Ext { get; set; }
        public string Names { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string MariedSurName { get; set; }
        //public DateTime BirthDate { get; set; }
        //public string Gender { get; set; }
        //public string Nationality { get; set; }
        //public string AFP { get; set; }
        //public string NUA { get; set; }
        public string Dependencia { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string State { get; set; }
        public int BranchesId { get; set; }
        public Branches Branches { get; set; }


        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_TempAlta_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}