using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace UserProfilerLab.Models
{
    public class ProfileModel
    {
        [Display(Name = "First Name")]
        public string ForeNames { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Display(Name = "Mobile No")]
        public string MobileNo { get; set; }

        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; }
    }
}