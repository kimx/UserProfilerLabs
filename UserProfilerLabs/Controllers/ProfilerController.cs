using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Profile;
using UserProfilerLab.Models;

namespace UserProfilerLab.Controllers
{
    public class ProfilerController : Controller
    {
        //
        // GET: /Profiler/
        public ActionResult Index()
        {
            return View();
        }

        private string userName = "Kim2";
        public ActionResult EditProfile()
        {
            ProfileBase _userProfile = ProfileBase.Create(userName);
            ProfileModel _profile = new ProfileModel();
            if (_userProfile.LastUpdatedDate > DateTime.MinValue)
            {
                _profile.ForeNames = Convert.ToString(_userProfile.GetPropertyValue("ForeNames"));
                _profile.LastName = Convert.ToString(_userProfile.GetPropertyValue("LastName"));
                _profile.Gender = Convert.ToString(_userProfile.GetPropertyValue("Gender"));
                _profile.MobileNo = Convert.ToString(_userProfile.GetProfileGroup("Contact").GetPropertyValue("MobileNo"));
                _profile.EmailAddress = Convert.ToString(_userProfile.GetProfileGroup("Contact").GetPropertyValue("EmailAddress"));
                // UserCondition userCondition = (UserCondition)_userProfile.GetPropertyValue("UserCondition");
                //UserCollection list = (UserCollection)_userProfile.GetPropertyValue("UserCollection");
            }
            return View(_profile);
        }

        [HttpPost]
        public ActionResult EditProfile(ProfileModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to register the user profile
                System.Web.Profile.ProfileBase profile = System.Web.Profile.ProfileBase.Create(userName, true);

                if (profile != null)
                {
                    profile.SetPropertyValue("Gender", model.Gender);
                    profile.SetPropertyValue("ForeNames", model.ForeNames);
                    profile.SetPropertyValue("LastName", model.LastName);
                    profile.GetProfileGroup("Contact").SetPropertyValue("MobileNo", model.MobileNo);
                    profile.GetProfileGroup("Contact").SetPropertyValue("EmailAddress", model.EmailAddress);

                    //var condition = new UserCondition
                    //{
                    //    Gender = model.Gender,
                    //    ForeNames = model.ForeNames
                    //    ,
                    //    LastName = model.LastName,
                    //    MobileNo = model.MobileNo,
                    //    EmailAddress = model.EmailAddress
                    //};

                    //  profile.SetPropertyValue("UserCondition", condition);
                    //var list = new UserCollection();
                    //for (int i = 0; i < 10; i++)
                    //{
                    //    list.Add(condition);
                    //}
                    // profile.SetPropertyValue("UserCollection", list);

                    profile.Save();
                }
                else
                {
                    ModelState.AddModelError("", "Error writing to Profile");
                }


            }
            return View(model);
        }
    }
}