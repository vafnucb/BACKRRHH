using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Web;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;
using DocumentFormat.OpenXml.InkML;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;

namespace UcbBack.Logic
{
    public class ADClass
    {
        // first install directory services dll
        //Install-Package System.DirectoryServices -Version 4.5.0
        //Install-Package System.DirectoryServices.AccountManagement -Version 4.5.0



        public string sDomain = "UCB.BO";
        public string Domain = "192.168.18.62";
        //public string Domain = "UCB.BO";
        private HanaValidator hanaval;

        public ADClass()
        {
            hanaval = new HanaValidator();
        }

        public void adddOrUpdate(People person, string tempPass = null, bool update=false)
        {
            var p = findUser(person);
            if (p == null)
            {
                addUser(person,tempPass);
            }
            else if (update)
            {
                updateUser(person);
            }
        }

        public void addUser(People person,string tempPass=null)
        {
            try
            {
                var contract = person.GetLastContract();

                var branchGroup = contract.Branches.ADGroupName;

                using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                    Domain,
                    "OU=" + contract.Branches.ADOUName + ",DC=UCB,DC=BO",
                    "ADMNALRRHH",
                    "Rrhh1234"))
                {
                    //ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                    using (UserPrincipal up = new UserPrincipal(ouContex))
                    {
                        var initials = person.FirstSurName.ToCharArray()[0].ToString() +
                                       person.Names.ToCharArray()[0].ToString();
                        up.DisplayName = person.GetFullName();
                        up.Name = up.DisplayName;                        
                        up.GivenName = person.Names;
                        //up.MiddleName = person.SecondSurName;
                        up.Surname = person.FirstSurName;
                        up.SamAccountName = getSamAcoutName(person);
                        up.UserPrincipalName = up.SamAccountName + "@UCB.BO";
                        up.SetPassword(tempPass==null?person.Document:tempPass); // user ChangePassword to change password lol
                        up.VoiceTelephoneNumber = person.PhoneNumber;
                        up.EmailAddress = person.UcbEmail;
                        up.EmployeeId = person.CUNI;
                        up.PasswordNeverExpires = true;
                        up.Enabled = true;
                        //up.ExpirePasswordNow();


                        up.Save();
                        AddUserToGroup(up.UserPrincipalName, contract.Branches.ADGroupName); // allways with UserPrincipalName to add to the group

                        if (up.GetUnderlyingObjectType() == typeof(DirectoryEntry))
                        {
                            using (var entry = (DirectoryEntry)up.GetUnderlyingObject())
                            {
                                entry.Properties["initials"].Value = initials;
                                entry.Properties["title"].Value = contract.Positions.Name;
                                entry.Properties["company"].Value = contract.Branches.Name;
                                //todo find a way to know who is the manager
                                //entry.Properties["manager"].Value = "NaN";
                                entry.Properties["department"].Value = contract.Dependency.Name;
                                entry.CommitChanges();
                            }
                        }
                    }
                }
            }
            catch (PrincipalExistsException e)
            {
                Console.WriteLine(e);
            }
            
        }

        public void updateUser(People person)
        {
            try
            {
                var contract = person.GetLastContract();

                var branchGroup = contract.Branches.ADGroupName;

                using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                    Domain,
                    "OU=" + contract.Branches.ADOUName + ",DC=UCB,DC=BO",
                    "ADMNALRRHH",
                    "Rrhh1234"))
                {
                    var usr = findUser(person);
                    using (UserPrincipal up = (UserPrincipal)usr)
                    {
                        var initials = person.FirstSurName.ToCharArray()[0].ToString() +
                                       person.Names.ToCharArray()[0].ToString();
                        up.GivenName = person.Names;
                        up.Surname = person.FirstSurName;
                        up.DisplayName = person.GetFullName();
                        up.Name = up.DisplayName;
                        //up.SamAccountName = getSamAcoutName(person);
                        //up.UserPrincipalName = up.SamAccountName + "@UCB.BO";
                        up.VoiceTelephoneNumber = person.PhoneNumber;
                        up.EmailAddress = person.UcbEmail;
                        up.EmployeeId = person.CUNI;
                        up.PasswordNeverExpires = true;
                        up.Enabled = true;

                        up.Save();
                        AddUserToGroup(up.UserPrincipalName, contract.Branches.ADGroupName); // allways with UserPrincipalName to add to the group

                        if (up.GetUnderlyingObjectType() == typeof(DirectoryEntry))
                        {
                            using (var entry = (DirectoryEntry)up.GetUnderlyingObject())
                            {
                                entry.Properties["initials"].Value = initials;
                                entry.Properties["title"].Value = contract.Positions.Name;
                                entry.Properties["company"].Value = contract.Branches.Name;
                                //todo find a way to know who is the manager
                                //entry.Properties["manager"].Value = "NaN";
                                entry.Properties["department"].Value = contract.Dependency.Name;
                                entry.CommitChanges();
                            }
                        }
                    }
                }
            }
            catch (PrincipalExistsException e)
            {
                Console.WriteLine(e);
            }

        }

        public string getSamAcoutName(People person)
        {
            
            var _context = new ApplicationDbContext();
            var personuser = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == person.Id);


            if (personuser!=null)
            {
                return personuser.UserPrincipalName.Split('@')[0];
            }
            // First attempt
            var SAN = hanaval.CleanText(person.Names).ToCharArray()[0].ToString() + "."
                      + hanaval.CleanText(person.FirstSurName)
                      + (!person.SecondSurName.IsNullOrWhiteSpace() ? ("." + hanaval.CleanText(person.SecondSurName).ToCharArray()[0].ToString()) : "");
            SAN = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(SAN.Replace(" ", "")));
            var UPN = SAN + "@" + sDomain;
            var search = _context.CustomUsers.Where(x => x.UserPrincipalName == UPN).ToList();
            if (search.Any())
            {
                // Second attempt
                SAN = hanaval.CleanText(person.Names).ToCharArray()[0].ToString() + hanaval.CleanText(person.Names).ToCharArray()[1].ToString() + "."
                        + hanaval.CleanText(person.FirstSurName)
                        + (!person.SecondSurName.IsNullOrWhiteSpace() ? ("." + hanaval.CleanText(person.SecondSurName).ToCharArray()[0].ToString()) : "");
                SAN = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(SAN.Replace(" ", "")));

                UPN = SAN + "@" + sDomain;
                search = _context.CustomUsers.Where(x => x.UserPrincipalName == UPN).ToList();
                if (search.Any())
                {
                    // Third attempt
                    SAN = hanaval.CleanText(person.Names).ToCharArray()[0].ToString() + hanaval.CleanText(person.Names).ToCharArray()[1].ToString() + "."
                          + hanaval.CleanText(person.FirstSurName)
                          + (!person.SecondSurName.IsNullOrWhiteSpace() ? ("." + hanaval.CleanText(person.SecondSurName).ToCharArray()[0].ToString() + hanaval.CleanText(person.SecondSurName).ToCharArray()[1].ToString()) : "");
                    SAN = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(SAN.Replace(" ", "")));

                    UPN = SAN + "@" + sDomain;
                    search = _context.CustomUsers.Where(x => x.UserPrincipalName == UPN).ToList();
                    if (search.Any())
                    {
                        // Fourth attempt
                        SAN = hanaval.CleanText(person.Names).ToCharArray()[0].ToString() + "."
                              + hanaval.CleanText(person.FirstSurName)
                              + (!person.SecondSurName.IsNullOrWhiteSpace() ? ("." + hanaval.CleanText(person.SecondSurName).ToCharArray()[0].ToString()) : "")
                              + (person.BirthDate.Day).ToString().PadLeft(2,'0');
                        SAN = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(SAN.Replace(" ", "")));

                        UPN = SAN + "@" + sDomain;
                        Random rnd = new Random();
                        while (_context.CustomUsers.Where(x => x.UserPrincipalName == UPN).ToList().Any())
                        {
                            // Last and final attempt

                            SAN = hanaval.CleanText(person.Names).ToCharArray()[0].ToString() + "."
                                                                + hanaval.CleanText(person.FirstSurName)
                                                                + (!person.SecondSurName.IsNullOrWhiteSpace() ? ("." + hanaval.CleanText(person.SecondSurName).ToCharArray()[0].ToString()) : "")
                                                                + (rnd.Next(1, 100)).ToString().PadLeft(2, '0');
                            SAN = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(SAN.Replace(" ", "")));
                        }
                    }
                }
            }

            //person.UserPrincipalName = UPN;

            var newUser = new CustomUser();
            newUser.Id = CustomUser.GetNextId(_context);
            newUser.PeopleId = person.Id;
            newUser.UserPrincipalName = UPN;
            _context.CustomUsers.Add(newUser);
            _context.SaveChanges();
            return SAN;
        }

        public bool memberOf(CustomUser user,string groupName)
        {
            using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234"))
            {
                ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                var u = findUser(user.People);
                GroupPrincipal group = GroupPrincipal.FindByIdentity(ouContex, groupName);
                if (group != null)
                {
                    return (u.IsMemberOf(group));
                }
            }

            return false;
        }

        public Principal findUser(People person)
        {
            Principal user;
            PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");
            ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
            UserPrincipal up = new UserPrincipal(ouContex);    
            up.EmployeeId = person.CUNI;
            PrincipalSearcher ps = new PrincipalSearcher(up);
            user = (UserPrincipal)ps.FindOne();
            return user;
        }

        public void enableUser(People person, bool active = true)
        {
            ApplicationDbContext _context = new ApplicationDbContext();
            var user = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == person.Id);
            try
            {
                PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                    Domain,
                    "ADMNALRRHH@UCB.BO",
                    "Rrhh1234");
                //PrincipalContext principalContext = new PrincipalContext(ContextType.Domain);

                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity
                    (ouContex, user.UserPrincipalName);

                userPrincipal.Enabled = active;
                userPrincipal.PasswordNeverExpires = true;

                userPrincipal.Save();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public List<Branches> getUserBranchesSLOW(CustomUser customUser)
        {
            var start = DateTime.Now;
            List<Branches> roles = new List<Branches>();
            PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");

            ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
            //UserPrincipal user = UserPrincipal.FindByIdentity(ouContex, customUser.UserPrincipalName);
            UserPrincipal user = new UserPrincipal(ouContex);
            user.SamAccountName = customUser.UserPrincipalName.Split('@')[0];
            user = new PrincipalSearcher(user).FindOne() as UserPrincipal;
            var finduser = DateTime.Now;
            //if (user != null)
            //{
                List<string> grps = new List<string>();
                var groups = user.GetGroups().ToList();
                var getgroups = DateTime.Now;
                foreach (var group in groups)
                {
                    grps.Add(group.Name);
                }
                var listgrps = DateTime.Now;

                var _context = new ApplicationDbContext();
                roles = _context.Branch.ToList().Where(x => grps.Contains(x.ADGroupName)).ToList();
                var end = DateTime.Now;

            //}

            var t1 = finduser - start;
            var t2 = getgroups - finduser;
            var t3 = listgrps - getgroups;
            var t4 = end - listgrps;

            return roles;
        }

        public List<Branches> getUserBranches(CustomUser customUser)
        {
            List<Branches> roles = new List<Branches>();
            DirectoryEntry obEntry = new DirectoryEntry("LDAP://UCB.BO" ,
                "ADMNALRRHH",
                "Rrhh1234");
            DirectorySearcher srch = new DirectorySearcher(obEntry,
                "(sAMAccountName=" + customUser.UserPrincipalName.Split('@')[0] + ")");

            SearchResult res = srch.FindOne();

            if (res != null)
            {
            DirectoryEntry obUser = new DirectoryEntry(res.Path, "ADMNALRRHH",
                "Rrhh1234");
                object obGroups = obUser.Invoke("Groups");
                List<string> grps = new List<string>();

                foreach (var group in obUser.Properties["memberOf"])
                {
                    var ss = "{'" + group.ToString().Replace("=", "':'").Replace(",", "','")+"'}";
                    var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(ss);
                    grps.Add(dic["CN"]);
                }

                var _context = new ApplicationDbContext();
                roles = _context.Branch.ToList().Where(x => grps.Contains(x.ADGroupName)).ToList();

            }

            return roles;
        }

        public List<Rol> getUserRols(CustomUser customUser)
        {
            List<Rol> roles = new List<Rol>();
            DirectoryEntry obEntry = new DirectoryEntry("LDAP://UCB.BO",
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");
            // obEntry.Username = "ADMNALRRHH";
            // obEntry.Password = "Rrhh1234";
            DirectorySearcher srch = new DirectorySearcher(obEntry,
                "(sAMAccountName=" + customUser.UserPrincipalName.Split('@')[0] + ")");

            SearchResult res = srch.FindOne();

            if (res != null)
            {
                DirectoryEntry obUser = new DirectoryEntry(res.Path, "ADMNALRRHH",
                    "Rrhh1234");
                object obGroups = obUser.Invoke("Groups");
                List<string> grps = new List<string>();

                foreach (var group in obUser.Properties["memberOf"])
                {
                    var ss = "{'" + group.ToString().Replace("=", "':'").Replace(",", "','") + "'}";
                    var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(ss);
                    grps.Add(dic["CN"]);
                }

                var _context = new ApplicationDbContext();
                roles = _context.Rols.Include(x => x.Resource).ToList().Where(x => grps.Contains(x.ADGroupName)).ToList();

            }

            return roles;
        }

        public List<Rol> getUserRolsSLOW(CustomUser customUser)
        {
            List<Rol> roles = new List<Rol>();
            PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");
            
            ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
            // UserPrincipal user = UserPrincipal.FindByIdentity(ouContex, customUser.UserPrincipalName);
            UserPrincipal user = new UserPrincipal(ouContex);
            user.SamAccountName = customUser.UserPrincipalName.Split('@')[0];
            user = new PrincipalSearcher(user).FindOne() as UserPrincipal;
            if (user != null)
            {
                List<string> grps = new List<string>();
                var groups = user.GetGroups();
                foreach (var group in groups)
                {
                    grps.Add(group.Name);
                }
                var _context = new ApplicationDbContext();
                roles = _context.Rols.Include(x=>x.Resource).ToList().Where(x => grps.Contains(x.ADGroupName)).ToList();
            }

            return roles;
        }

        public void AddUserToGroup(string userPrincipalName, string groupName)
        {
            try
            {
                using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                    Domain,
                    "ADMNALRRHH@UCB.BO",
                    "Rrhh1234"))
                {
                    ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(ouContex, groupName);
                    group.Members.Add(ouContex, IdentityType.UserPrincipalName, userPrincipalName);
                    group.Save();
                }
            }
            catch (System.DirectoryServices.DirectoryServicesCOMException e)
            {
                //doSomething with E.Message.ToString(); 
                Console.WriteLine(e);
            }
        }
        public void RemoveUserFromGroup(string userPrincipalName, string groupName)
        {
            try
            {
                using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                    Domain,
                    "ADMNALRRHH@UCB.BO",
                    "Rrhh1234"))
                {
                    ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(ouContex, groupName);
                    group.Members.Remove(ouContex, IdentityType.UserPrincipalName, userPrincipalName);
                    group.Save();
                }
            }
            catch (System.DirectoryServices.DirectoryServicesCOMException e)
            {
                //doSomething with E.Message.ToString(); 
                Console.WriteLine(e);
            }
        }

        public bool ActiveDirectoryAuthenticate(string username, string password)
        {

            PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");
                bool Valid = ouContex.ValidateCredentials(username, password);
                if (Valid)
                {
                    ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                    UserPrincipal up = new UserPrincipal(ouContex);
                        up.UserPrincipalName = username;
                        PrincipalSearcher ps = new PrincipalSearcher(up);
                        
                            var user = (UserPrincipal) ps.FindOne();
                            
                            return user == null ? false : user.Enabled.Value;
                        
                    }

                return false;
            
        }

        public List<string> getGroups()
        {
            List<string> res = new List<string>();
            PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH@UCB.BO",
                "Rrhh1234");
            ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
            GroupPrincipal qbeGroup = new GroupPrincipal(ouContex);
            
            PrincipalSearcher srch = new PrincipalSearcher(qbeGroup);

            

            foreach (var group in srch.FindAll())
            {
                res.Add(((GroupPrincipal)group).Name);   
            }

            return res;

        }

        public bool createGroup(string name)
        {
            using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "OU=Personas,DC=UCB,DC=BO",
                "ADMNALRRHH",
                "Rrhh1234"))
            {
                ouContex.ValidateCredentials("ADMNALRRHH", "Rrhh1234");
                GroupPrincipal group = GroupPrincipal.FindByIdentity(ouContex, name);
                if (group == null)
                {
                    using (GroupPrincipal up = new GroupPrincipal(ouContex))
                    {
                        up.IsSecurityGroup = false;
                        up.Name = name;
                        up.DisplayName = name;
                        up.GroupScope = GroupScope.Global;
                        up.Save();
                    }

                    return true;
                }

                return false;
            }
        }

        /*public bool ChangePassword(CustomUser customUser, string oldpassword, string newpassword)
        {
            UserPrincipal user = null;
            using (PrincipalContext ouContex = new PrincipalContext(ContextType.Domain,
                Domain,
                "ADMNALRRHH",
                "Rrhh1234"))
            {
                using (UserPrincipal up = new UserPrincipal(ouContex))
                {
                    up.EmployeeId = person.CUNI;
                    using (PrincipalSearcher ps = new PrincipalSearcher(up))
                    {
                        user = (UserPrincipal)ps.FindOne();
                    }
                }
            }

            if (user == null) return false;
            if (!ActiveDirectoryAuthenticate(person.UserPrincipalName, oldpassword)) return false;
            try
            {
                user.ChangePassword(oldpassword, newpassword);
                return true;
            }
            catch (Exception e)
            {
                return false;
            } 
        }*/


    }
}