﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Serv
{
    [CustomSchema("Serv_Varios")]
    public class Serv_Varios
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }
        public int Serv_ProcessId { get; set; }

        public string CardCode { get; set; }
        public string CardName { get; set; }
        public int DependencyId { get; set; }
        public Dependency Dependency { get; set; }
        public string PEI { get; set; }

        public string ServiceName { get; set; }
        // line Memo
        public string ContractObjective { get; set; }
        public string AssignedAccount { get; set; }
        public Decimal ContractAmount { get; set; }
        public Decimal IUE { get; set; }
        public Decimal IT { get; set; }
        public Decimal TotalAmount { get; set; }
        public string Comments { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Serv_Varios_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}