using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class ContractDetailForWatch
    {
        public int Id { set; get; }

        public string CUNI { get; set; }
        public string Document { get; set; }

        public string FullName { get; set; }
        public int PeopleId { get; set; }

        public int DependencyId { get; set; }
        public string Dependency { get; set; }

        public int PositionsId { get; set; }
        public string Positions { get; set; }

        public string PositionDescription { get; set; }
        public string Dedication { get; set; }

        public int Linkage { get; set; }

        public string Linkagestr { get; set; }
        public int BranchesId { get; set; }
        public string Branches { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? EndDateNombramiento { get; set; }
        public Boolean Active { get; set; }
        public bool AI { get; set; }

        public string Cause { get; set; }
        public string NumGestion { get; set; }
        public string Seguimiento { get; set; }
        public string Respaldo { get; set; }
        public string Comunicado { get; set; }

        public string ValidoPara { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
    }
}