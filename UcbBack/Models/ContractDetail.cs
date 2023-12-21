using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("ContractDetail")]
    public class ContractDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }
        
        public string CUNI { get; set; }

        public int PeopleId { get; set; }
        public People People { get; set; }

        public int DependencyId { get; set; }
        public Dependency Dependency { get; set; }

        public int PositionsId { get; set; }
        public Positions Positions { get; set; }

        public string PositionDescription { get; set; }
        public string Dedication { get; set; }

        public TableOfTables Link { get; set; }
        [ForeignKey("Link")]
        public int Linkage { get; set; }

        public int BranchesId { get; set; }
        public Branches Branches { get; set; }

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }
        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }
        [Column(TypeName = "date")]
        public DateTime? EndDateNombramiento { get; set; }
        public Boolean Active { get; set; }
        public bool AI { get; set; }

        public string Cause { get; set; }
        public string NumGestion { get; set; }
        public string Seguimiento { get; set; }
        public string Respaldo { get; set; }
        public string Comunicado { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT " + CustomSchema.Schema + ".\"rrhh_ContractDetail_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}