using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using UcbBack.Models;
using System.Data.Entity;
using Microsoft.Ajax.Utilities;
using UcbBack.Models.Auth;

namespace UcbBack.Logic
{
    public class ValidatePerson
    {
        private ApplicationDbContext _context;
        private HanaValidator hanaValidator;

        public ValidatePerson(ApplicationDbContext context=null)
        {

            _context = context?? new ApplicationDbContext();
            hanaValidator = new HanaValidator(_context);
        }

        public People CleanName(People person)
        {
            person.FirstSurName = hanaValidator.CleanText(person.FirstSurName);
            person.SecondSurName = hanaValidator.CleanText(person.SecondSurName);
            person.Names = hanaValidator.CleanText(person.Names);
            person.MariedSurName = hanaValidator.CleanText(person.MariedSurName);

            return person;
        }
        //Actualizacion de validacion para incluir la nueva tabla salomon
        //todo seems to work for me
        public bool IsActive(People person,string date = null,string format ="yyyy-MM-dd",int branchId=-1)
        {
            try
            {
                DateTime toDate = date == null
                    ? DateTime.Now
                    : DateTime.ParseExact(date, format, System.Globalization.CultureInfo.InvariantCulture);
                bool xw;
                int dd;
                var cd = _context.ContractDetails.ToList();
                var vl = _context.ValidoSalomons.ToList();

                var query = from valid in vl
                    join cont in cd on valid.ContractDetailId equals cont.Id
                    select cont;

                var contractDetails = query.ToList();
                if(branchId==-1)
                {
                     dd = toDate.Year * 100 + toDate.Month;
                     
                     xw = contractDetails.Where(x => x.CUNI == person.CUNI).ToList()
                        .Any(x =>
                            (
                                x.StartDate.Year * 100 + x.StartDate.Month <= toDate.Year * 100 + toDate.Month

                                && 
                                (
                                    x.EndDate == null
                                    || (x.EndDate.Value.Year * 100 + x.EndDate.Value.Month >= toDate.Year * 100 + toDate.Month)
                                )
                             )
                        );
                    var t = contractDetails.Where(x => x.CUNI == person.CUNI).ToList()
                        .Where(x =>
                            (
                                x.StartDate.Year * 100 + x.StartDate.Month <= toDate.Year * 100 + toDate.Month
                                &&
                                (
                                    x.EndDate == null
                                    || x.EndDate.Value.Year * 100 + x.EndDate.Value.Month >= toDate.Year * 100 + toDate.Month
                                )
                            )
                        );
                }
                else
                {
                    xw = contractDetails.Where(x => x.CUNI == person.CUNI).ToList().Any(x =>
                        (x.StartDate.Month <= toDate.Month 
                         && x.StartDate.Year <= toDate.Year 
                         && (x.EndDate == null 
                             || (x.EndDate.Value.Month >= toDate.Month 
                                 && x.EndDate.Value.Year >=toDate.Year)) 
                         && x.BranchesId == branchId));
                }

                if (!xw)
                    return false;
                return xw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            
        }
        //Ahora probando este buen dep
        public bool IspersonDependency(People person, string dependencyCod, string date = null,
            string format = "yyyy-MM-dd")
        {
            var cd = _context.ContractDetails.ToList();
            var vl = _context.ValidoSalomons.ToList();
            var dep = _context.Dependencies.ToList();

            var query = from valid in vl
                join cont in cd on valid.ContractDetailId equals cont.Id
                join d in dep on cont.DependencyId equals d.Id
                select new {cont.CUNI, cont.StartDate, cont.EndDate, Dependency = d.Cod};
            var contractDetails = query.ToList();
            DateTime toDate = date == null
                ? DateTime.Now
                : DateTime.ParseExact(date, format, System.Globalization.CultureInfo.InvariantCulture);
            bool xw = contractDetails.ToList()
                .Any(x =>
                    (
                        x.CUNI == person.CUNI
                        && x.StartDate.Year * 100 + x.StartDate.Month <= toDate.Year * 100 + toDate.Month
                        &&
                        (
                            x.EndDate == null
                            || x.EndDate.Value.Year * 100 + x.EndDate.Value.Month >= toDate.Year * 100 + toDate.Month)
                        && x.Dependency == dependencyCod)
                );
            if (!xw)
                return false;
            return xw;
        }

        public bool IspersonBranch(People person, string dependencyCod, int branch, string date = null, string format = "yyyy-MM-dd")
        {
            var cd = _context.ContractDetails.ToList();
            var vl = _context.ValidoSalomons.ToList();
            var dep = _context.Dependencies.ToList();

            var query = from valid in vl
                join cont in cd on valid.ContractDetailId equals cont.Id
                join d in dep on cont.DependencyId equals d.Id
                select new { cont.CUNI, cont.StartDate, cont.EndDate, Dependency = d.Cod, cont.BranchesId };
            var contractDetails = query.ToList();
            DateTime toDate = date == null
                ? DateTime.Now
                : DateTime.ParseExact(date, format, System.Globalization.CultureInfo.InvariantCulture);
            bool xw = contractDetails.ToList()
                .Any(x =>
                    (
                        x.CUNI == person.CUNI
                        && x.StartDate.Year * 100 + x.StartDate.Month <= toDate.Year * 100 + toDate.Month
                        &&
                        (
                            x.EndDate == null
                            || x.EndDate.Value.Year * 100 + x.EndDate.Value.Month >= toDate.Year * 100 + toDate.Month)
                        && x.BranchesId == branch)
                );
            if (!xw)
                return false;
            return xw;
        }
        

        public string GetConfirmationToken(IEnumerable<People> people)
        {
            string token = "";

            foreach (var p in people)
            {
                token += p.CUNI;
            }

            byte[] encodedstr = new UTF8Encoding().GetBytes(token);
            byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedstr);
            token = Convert.ToBase64String(hash);

            return token;
        }

        public People UcbCode(People person,CustomUser user)
        {
            DateTime bd = person.BirthDate;
            var daystr = person.BirthDate.ToString("dd");
            var monthstr = person.BirthDate.ToString("MM");
            var yearstr = person.BirthDate.ToString("yy");

            char[] letras =
            {
                person.FirstSurName[0], 
                person.SecondSurName.IsNullOrWhiteSpace()? person.FirstSurName[1] : person.SecondSurName[0], 
                person.Names[0], 
                //#'-',
                //year
                yearstr[0],
                yearstr[1],
                //month
                monthstr[0],
                monthstr[1],
                //day
                daystr[0],
                daystr[1]
            };

            person.CUNI = new string(letras);
            //collision!
            while ((_context.Person.FirstOrDefault(p => p.CUNI == person.CUNI)) != null)
            {
                //register error log number 9 (CUNI Collision)
                SystemErrorLogs log = new SystemErrorLogs(user,person,9);
                // calculate other CUNI
                char[] monthi =
                {
                    person.CUNI[5], person.CUNI[6]
                };
                int newmonth = Int32.Parse(new string(monthi)) > 12 ? Int32.Parse(new string(monthi)) + 10 : Int32.Parse(new string(monthi)) + 20;
                char[] oldCodArray = person.CUNI.ToCharArray();
                oldCodArray[5] = newmonth.ToString()[0];
                oldCodArray[6] = newmonth.ToString()[1];
                person.CUNI = new string(oldCodArray);
            }
            return person;
        }

        public IEnumerable<People> VerifyExisting(People person, float n,string fn=null)
        {
            string fullname;
            if (fn == null)
            {
                fullname = String.Concat(person.FirstSurName,
                    String.Concat(" ",
                        String.Concat(person.SecondSurName,
                            String.Concat(" ",
                                String.Concat(person.Names,
                                    String.Concat(" ", person.Document)
                                )
                            )
                        )
                    )
                );
            }
            else
                fullname = fn;
            
            //SQL command in Hana
            string colToCompare = "concat(a.\"FirstSurName\"," +
                                "concat('' '',"+
                                    "concat(a.\"SecondSurName\","+
                                        "concat('' '',"+
                                            "concat(a.\"Names\", "+
                                                "concat('' '',a.\"Document\")"+
                                            ")"+
                                        ")"+
                                    ")"+
                                ")"+
                            ")";
            string colId = "a.\"CUNI\"";
            string table = "People";
            
            var similarities = hanaValidator.Similarities(fullname, colToCompare, table, colId, 0.9f);

            var sim = _context.Person.ToList().Where(
                i => similarities.Contains(i.CUNI)
            ).ToList();
            return sim;
        }
    }
}