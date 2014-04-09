using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace UserProfilerLab.Models
{
  //  [XmlInclude(typeof(BaseModel))]
    [Serializable]
    public class UserCollection : List<BaseModel>
    {



    }
}