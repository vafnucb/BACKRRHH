using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("BranchhasPosition")]
    public class BranchhasPosition
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int BranchesId { get; set; }
        public Branches Branches { get; set; }
        public int PositionId { get; set; }
        public Positions Positions { get; set; }
        public int NumberOfPositions { get; set; }
        public bool Enabled { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_BranchhasPosition_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}