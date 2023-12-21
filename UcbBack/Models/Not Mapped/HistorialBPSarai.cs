using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class HistorialBPSarai
    {
        public int Serv_ProcessId { set; get; }
        public string CardCode { get; set; }
        public string CardName { get; set; }
        public int DependencyId { set; get; }
        public string PEI { get; set; }
        public string ServiceName { get; set; }
        public decimal? ContractAmount { get; set; }
        public decimal? IUE { get; set; }
        public decimal? IT { get; set; }
        public decimal? TotalAmount { get; set; }
        public int BranchesId { set; get; }
        public string Regional { set; get; }
        public string Branch { set; get; }
        public string FileType { set; get; }
        public string SAPId { set; get; }
        public string InSAPAt { set; get; }
        public string Comments { set; get; }
        public string Dependency { set; get; }
    }
}