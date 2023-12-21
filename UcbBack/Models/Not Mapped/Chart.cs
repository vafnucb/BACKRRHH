using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class Chart
    {
        public string PARENT7_ID { set; get; }
        public string PARENT6_ID { set; get; }
        public string PARENT5_ID { set; get; }
        public string PARENT4_ID { set; get; }
        public string PARENT3_ID { set; get; }
        public string PARENT2_ID { set; get; }
        public string Dep { get; set; }
        public string Regional { get; set; }
        public string Cod7 { get; set; }
        public string Cod6 { get; set; }
        public string Cod5 { get; set; }
        public string Cod4 { get; set; }
        public string Cod3 { get; set; }
        public string Cod2 { get; set; }
        public string Cod { get; set; }
        public int BranchesId { get; set; }
        public int Id { get; set; }
    }
}