using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_Payroll")]
    public class Dist_Payroll
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }
        public string Document { get; set; }
        public string Names { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string MariedSurName { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal AntiquityBonus { get; set; }
        public decimal OtherIncome { get; set; }
        public decimal TeachingIncome { get; set; }
        public decimal OtherAcademicIncomes { get; set; }
        public decimal Reintegro { get; set; }
        public decimal TotalAmountEarned { get; set; }
        public string AFP { get; set; }
        public decimal AFPLaboral { get; set; }
        public decimal AFPPatronal { get; set; }
        public decimal RcIva { get; set; }
        public decimal Discounts { get; set; }
        public decimal TotalAmountDiscounts { get; set; }
        public decimal TotalAfterDiscounts { get; set; }
        public string CUNI { get; set; }
        public string EmployeeType { get; set; }
        public string PEI { get; set; }
        public double WorkedHours { get; set; }
        public string Dependency { get; set; }
        public decimal SeguridadCortoPlazoPatronal { get; set; }
        public string IdentificadorSSU { get; set; }
        public decimal ProvAguinaldo { get; set; }
        public decimal ProvPrimas { get; set; }
        public decimal ProvIndeminizacion { get; set; }
        public string ProcedureTypeEmployee { get; set; }
        public decimal Porcentaje { get; set; }
        public string segmentoOrigen { get; set; }
        [StringLength(2)]
        public string mes { get; set; }
        [StringLength(4)]
        public string gestion { get; set; }

        public Dist_File DistFile { get; set; }
        public long DistFileId { get; set; }
        public string ModoPago { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dist_Payroll_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}