using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class FiltroBG
    {
        public int Id { set; get; }
        public string Cod { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public int BranchesId { get; set; }
        public string Branch { get; set; }
        public int OrganizationUnitId { get; set; }
        public string OrganizationUnit { get; set; }
    }
}