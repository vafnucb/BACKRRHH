using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Linq;
using System.Web;
using UcbBack.Logic;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Civil")]
    public class Civil
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string FullName { get; set; }
        public string SAPId { get; set; }
        public string NIT { get; set; }
        public string Document { get; set; }
        public int CreatedBy { get; set; }
        public int BranchesId { get; set; }
        [NotMapped] public Branches Branches { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Civil_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
        public static IQueryable<Civil> findBPInSAP(string CardCode, CustomUser user, ApplicationDbContext _context)
        {
            string condicion = "";
            // Si el codigo de socio comienza con P o H, entonces se busca en socios de negocio, sino es busqueda por CI
            if (CardCode.Substring(0, 1).Equals("H") || CardCode.Substring(0, 1).Equals("P"))
            {
                condicion = " and ocrd.\"CardCode\"= '" + CardCode + "'";
            }
            else {
                condicion = " and ocrd.\"LicTradNum\"= '" + CardCode + "'";
            }
            var auth = new ValidateAuth();
            var query = "select 0 \"Id\",0 \"CreatedBy\",null \"Document\", ocrd.\"CardCode\" \"SAPId\", ocrd.\"CardName\" \"FullName\",ocrd.\"LicTradNum\" \"NIT\", br.\"Id\" \"BranchesId\"" +
                        " from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd" +
                        " inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".crd8" +
                        " on ocrd.\"CardCode\" = crd8.\"CardCode\"" +
                        " inner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        " on br.\"CodigoSAP\" = crd8.\"BPLId\"" +
                        " where ocrd.\"validFor\" = 'Y'" +
                        " and ocrd.\"frozenFor\" = 'N'" +
                        " and crd8.\"DisabledBP\" = 'N'" +
                        " and ocrd.\"CardType\" = 'S'" +
                        condicion;

            var rawresult = _context.Database.SqlQuery<Civil>(query).ToList();

            if (rawresult.Count() == 0)
                return null;

            var res = auth.filerByRegional(rawresult.AsQueryable(), user);
            if (res.Count() == 0)
                return null;

            return res.Cast<Civil>();
        }
    }
}