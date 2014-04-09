   ﻿using System;
using System.Data;
using System.Configuration;
using System.Configuration.Provider;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.Profile;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Hosting;
using System.Data.SqlTypes;
using System.Collections.Specialized;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.IO;

public class SearchableSqlProfileProvider :SqlProfileProvider
{
    private String _name = "SearchableSqlProfileProvider";
    private String _description = "Extends the SqlProfileProvider to support searching through properties.";
    private String _connectionStringName;
    private Int32 _commandTimeout;

    public String ConnectionStringName
    {
        get
        {
            return _connectionStringName;
        }
    }

    public Int32 CommandTimeout
    {
        get
        {
            return _commandTimeout;
        }
    }

    public override void Initialize(String name, NameValueCollection config)
    {
        // if the parameter name is empty, use the default name
        if (name == null || name.Trim() == String.Empty)
        {
            name = _name;
        }

        // if no configuration attributes found, throw an exception
        if (config == null)
        {
            throw new ArgumentNullException("config", "There are no configuration attributes in web.config!");
        }

        // if no 'description' attribute in the configuration, use the default
        String cfg_description = config["description"];
        if (cfg_description == null || cfg_description.Trim() == "")
        {
            config.Remove("description");
            config.Add("description", _description);
        }

        // if there is no 'connectionStringName' in the configuration, throw an exception
        // otherwise extract the connection string from the web.config
        // and test it, if it is not working throw an exception
        String cfg_connectionStringName = config["connectionStringName"];
        if (cfg_connectionStringName == null || cfg_connectionStringName.Trim() == "")
        {
            throw new ProviderException("Provider configuration attribute 'connectionStringName' in web.config is missing or blank!");
        }
        else
        {
            // get the entry refrenced by the 'connectionStringName'
            ConnectionStringSettings connObj = ConfigurationManager.ConnectionStrings[cfg_connectionStringName];

            // if you can't find the entry defined by the 'connectionStringName' or it is empty, throw an exception
            if (connObj != null && connObj.ConnectionString != null && connObj.ConnectionString.Trim() != "")
            {
                try
                {
                    // try testing the connection
                    using (SqlConnection conn = new SqlConnection(connObj.ConnectionString))
                    {
                        conn.Open();
                        _connectionStringName = connObj.ConnectionString;
                    }
                }
                catch (Exception e)
                {
                    // if anything wrong happened, throw an exception showing what happened
                    ProviderException pException = new ProviderException(
                        String.Format("Connection string '{0}' in web.config is not usable!", cfg_connectionStringName), e);

                    throw pException;
                }
            }
            else
            {
                throw new ProviderException(String.Format("Connection string '{0}' in web.config is missing or blank!",
                    cfg_connectionStringName));
            }
        }

        // if there is no 'commandTimeout' attribute in the configuration, use the default
        // otherwise try to get it, if errors ocurred, throw an exception
        String cfg_commandTimeout = config["commandTimeout"];
        if (cfg_commandTimeout == null || cfg_commandTimeout.Trim() == String.Empty)
        {
         
        
        }
        else
        {
            Int32 _ct;

            if (Int32.TryParse(cfg_commandTimeout, out _ct) && _ct >= 0)
            {
                _commandTimeout = _ct;
            }
            else
            {
                throw new ProviderException("Provider property 'commandTimeout' in web.config is not valid!");
            }
        }

        // initialize the SqlProfileProvider with the current parameters
        base.Initialize(name, config);

        // throw an exception if unrecognized attributes remain
        if (config.Count > 0)
        {
            String strAttributes = "";

            for (int i = 0; i < config.Count; i++)
            {
                strAttributes += config.GetKey(i);

                if (i < config.Count - 1)
                {
                    strAttributes += ", ";
                }
            }

            throw new ProviderException(String.Format("Unrecognized attribute(s): {0}", strAttributes));
        }
    }

    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext sc, SettingsPropertyCollection properties)
    {
        // create the collection to return
        SettingsPropertyValueCollection spvc = new SettingsPropertyValueCollection();

        #region Check The Function Parameters Are Valid
        // check we have the user profile information.
        if (sc == null)
        {
            return spvc;
        }

        // check we do have the profile information for the properties to be retrieved.
        // check we do have properties in the profile
        if (properties == null || properties.Count < 1)
        {
            return spvc;
        }

        // get the username
        // get if the user is authenticated
        // if the username is null or empty, return empty property value collection
        Boolean isAuthenticated = (Boolean)sc["IsAuthenticated"];
        String username = (String)sc["UserName"];
        if (String.IsNullOrEmpty(username))
        {
            return spvc;
        }
        #endregion

        #region Fill the collection to return with the profile properties initialized to their default values
        foreach (SettingsProperty sp in properties)
        {
            // If the serialization is up to us to decide, try and see if it can be serialised as a string
            // otherwise serialise as XML
            if (sp.SerializeAs == SettingsSerializeAs.ProviderSpecific)
            {
                // If it is a primitive type or a string, then just store it as a string
                if (sp.PropertyType.IsPrimitive || (sp.PropertyType == typeof(string)))
                {
                    sp.SerializeAs = SettingsSerializeAs.String;
                }
                else // Else serialize it as XML
                {
                    sp.SerializeAs = SettingsSerializeAs.Xml;
                }
            }

            // create a property value based on the profile property settings, including default value
            // Add the property value to the collection to return
            spvc.Add(new SettingsPropertyValue(sp));
        }
        #endregion

        #region Retrieve the stored property values from the database
        try
        {
            GetNonDefaultPropertyValuesForUser(username, spvc);
        }
        catch (Exception e)
        {
            // if anything went wrong, throw an exception
            throw new ProviderException(String.Format(
                "Error getting profile property values from database.\nUsername: '{0}'\nIs Authenticated: {1}",
                username, isAuthenticated.ToString()), e);
        }
        #endregion

        return spvc;
    }

    public override void SetPropertyValues(SettingsContext sc, SettingsPropertyValueCollection properties)
    {
        #region Check The Function Parameters Are Valid
        // check we have the user profile information.
        if (sc == null)
        {
            return;
        }

        // check we do have the profile information for the properties to be retrieved.
        // check we do have properties in the profile
        if (properties == null || properties.Count < 1)
        {
            return;
        }

        // get the username
        // get if the user is authenticated
        // if the username is null or empty, quit
        String username = (String)sc["UserName"];
        Boolean isAuthenticated = (Boolean)sc["IsAuthenticated"];
        if (String.IsNullOrEmpty(username))
        {
            return;
        }
        #endregion

        #region Build A List Of Items To Be Actually Saved
        // build a list
        List<SettingsPropertyValue> columnData = new List<SettingsPropertyValue>(properties.Count);

        // add all properties to be saved to the list
        foreach (SettingsPropertyValue spv in properties)
        {
            // iIf the property is dirty "written to or used", add it to the list
            if (spv.IsDirty)
            {
                // if the user is anonymous and the property is not marked "AllowAnonymous", skip this property
                if (!isAuthenticated && !((Boolean)spv.Property.Attributes["AllowAnonymous"]))
                {
                    continue;
                }

                // add the property to the list
                columnData.Add(spv);
            }
        }

        // if the list is empty, quit
        if (columnData.Count < 1)
        {
            return;
        }
        #endregion

        #region Save The List To The Database
        try
        {
            // make a conection
            using (SqlConnection conn = new SqlConnection(_connectionStringName))
            {
                // set the command object to the stored procedure used to save the properties
                // set the command to use the connection
                // set the command timeout value
                // initialize the common stored procedure parameters to their values
                SqlCommand SetPropertyCommand = new SqlCommand("aspnet_Profile_SetProperty", conn);
                SetPropertyCommand.CommandTimeout = _commandTimeout;
                SetPropertyCommand.CommandType = CommandType.StoredProcedure;
                SetPropertyCommand.Parameters.Add("@ApplicationName", SqlDbType.NVarChar, 256).Value = base.ApplicationName;
                SetPropertyCommand.Parameters.Add("@PropertyName", SqlDbType.NVarChar, 256);
                SetPropertyCommand.Parameters.Add("@PropertyUsingDefaultValue", SqlDbType.Bit);
                SetPropertyCommand.Parameters.Add("@PropertyValueString", SqlDbType.NVarChar, Int32.MaxValue);
                SetPropertyCommand.Parameters.Add("@PropertyValueBinary", SqlDbType.VarBinary, Int32.MaxValue);
                SetPropertyCommand.Parameters.Add("@UserName", SqlDbType.NVarChar, 256).Value = username;
                SetPropertyCommand.Parameters.Add("@IsUserAnonymous", SqlDbType.Bit).Value = !isAuthenticated;
                SetPropertyCommand.Parameters.Add("@CurrentTimeUtc", SqlDbType.DateTime).Value = DateTime.UtcNow;

                // for each property on the list
                foreach (SettingsPropertyValue spv in columnData)
                {
                    // set the rest of the stored procedure parameters according to each specific property features
                    SetPropertyCommand.Parameters["@PropertyName"].Value = spv.Property.Name;

                    // if the property is using the same value as DdefaultValue
                    // otherwise continue your normal insert/update procedure
                    if (spv.Property.DefaultValue.Equals(spv.SerializedValue) == true)
                    {
                        // just mark the procedures parameter @PropertyUsingDefaultValue to true
                        // to remove the entry from the database so it switches back to default
                        // the other values are not really significant so use DBNull
                        SetPropertyCommand.Parameters["@PropertyUsingDefaultValue"].Value = 1;
                        SetPropertyCommand.Parameters["@PropertyValueString"].Value = DBNull.Value;
                        SetPropertyCommand.Parameters["@PropertyValueBinary"].Value = DBNull.Value;
                    }
                    else
                    {
                        // set the parameter @PropertyUsingDefaultValue to false so the procedure would insert/update
                        SetPropertyCommand.Parameters["@PropertyUsingDefaultValue"].Value = 0;

                        // if the property value is null, set both string and binary values to null
                        // otherwise set the appropriate one to the property value after it has been serialized properly
                        if ((spv.Deserialized && spv.PropertyValue == null) || (!spv.Deserialized && spv.SerializedValue == null))
                        {
                            SetPropertyCommand.Parameters["@PropertyValueString"].Value = DBNull.Value;
                            SetPropertyCommand.Parameters["@PropertyValueBinary"].Value = DBNull.Value;
                        }
                        else
                        {
                            // get the serialized property value
                            //spv.SerializedValue = SerializePropertyValue(spv.Property, spv.PropertyValue);

                            // set the approporiate parameter of the stored procedure to the serialized value
                            // if the serialized value is a string store it in the @PropertyValueString parameter and set the other to DBNull
                            // otherwise store the value in the @PropertyValueBinary and set the other to DBNull
                            if (spv.SerializedValue is String)
                            {
                                SetPropertyCommand.Parameters["@PropertyValueString"].Value = spv.SerializedValue;
                                SetPropertyCommand.Parameters["@PropertyValueBinary"].Value = DBNull.Value;
                            }
                            else
                            {
                                SetPropertyCommand.Parameters["@PropertyValueString"].Value = DBNull.Value;
                                SetPropertyCommand.Parameters["@PropertyValueBinary"].Value = spv.SerializedValue;
                            }
                        }
                    }

                    // if the connection is closed, open it
                    if (conn.State == ConnectionState.Closed)
                    {
                        conn.Open();
                    }

                    // execute the stored procedure
                    // if no rows were affected, then throw an error, because nothing has been saved!
                    if (SetPropertyCommand.ExecuteNonQuery() == 0)
                    {
                        throw new ProviderException(String.Format("Updating the profile property '{0}' in the database failed!", spv.Name));
                    }
                }
            }
        }
        catch (Exception e)
        {
            // if anything went wrong, throw an exception
            throw new ProviderException(String.Format(
                "Error setting profile property value to database.\nUsername: '{0}'\nIs Authenticated: {1}",
                username, isAuthenticated.ToString()), e);
        }
        #endregion
    }

    #region Search Interface
    public enum SearchOperator
    {
        Equal = 0,
        NotEqual = 1,
        Like = 2,
        NotLike = 3,
        LessThan = 4,
        LessThanOEqual = 5,
        GreaterThan = 6,
        GreaterThanOrEqual = 7,
        Contains = 8,
        FreeText = 9
    }

    public ProfileInfoCollection FindProfilesByPropertyValue(SettingsProperty property, SearchOperator searchOperator, object value)
    {
        // instantiate an empty ProfileInfoCollection to use it for return
        ProfileInfoCollection pic = new ProfileInfoCollection();

        // try and open the connection and get the results
        try
        {
            // get the connection we're going to use
            using (SqlConnection conn = new SqlConnection(_connectionStringName))
            {

                // instantiate a SettingsPropertyValue from the property 
                // to use it to serialize the value for comparison with the database
                SettingsPropertyValue spv = new SettingsPropertyValue(property);
                spv.PropertyValue = value;

                // set common parameters of the aspnet_Profile_FindProfiles stored procedure
                SqlCommand FindProfilesCommand = new SqlCommand("aspnet_Profile_FindProfiles", conn);
                FindProfilesCommand.CommandType = CommandType.StoredProcedure;
                FindProfilesCommand.CommandTimeout = _commandTimeout;
                FindProfilesCommand.Parameters.Add("@ApplicationName", System.Data.SqlDbType.NVarChar, 256).Value = base.ApplicationName;
                FindProfilesCommand.Parameters.Add("@PropertyName", System.Data.SqlDbType.NVarChar, 256).Value = property.Name;
                FindProfilesCommand.Parameters.Add("@OperatorType", System.Data.SqlDbType.Int).Value = (Int32)searchOperator;

                // if the serialized property value is of type string
                // carry on if the size is within allowed limits
                Boolean bTooBig = false;
                if (spv.SerializedValue is String)
                {
                    // if the serialized value is bigger than the PropertyValueString column's length
                    if (((String)spv.SerializedValue).Length > Int32.MaxValue)
                    {
                        bTooBig = true;
                    }
                    else
                    {
                        if (searchOperator == SearchOperator.Contains || searchOperator == SearchOperator.FreeText)
                        {
                            // if the searchOperator is a freetext operator then pass the value unaltered
                            FindProfilesCommand.Parameters.Add("@PropertyValueString",
                                System.Data.SqlDbType.NVarChar, Int32.MaxValue).Value = spv.PropertyValue;
                            FindProfilesCommand.Parameters.Add("@PropertyValueBinary",
                                System.Data.SqlDbType.VarBinary, Int32.MaxValue).Value = DBNull.Value;
                        }
                        else
                        {
                            // otherwise serialise the value before passing it
                            FindProfilesCommand.Parameters.Add("@PropertyValueString",
                                System.Data.SqlDbType.NVarChar, Int32.MaxValue).Value = spv.SerializedValue;
                            FindProfilesCommand.Parameters.Add("@PropertyValueBinary",
                                System.Data.SqlDbType.VarBinary, Int32.MaxValue).Value = DBNull.Value;
                        }

                        // set the parameter @PropertyType to indicate the property is a string
                        FindProfilesCommand.Parameters.Add("@PropertyType", System.Data.SqlDbType.Bit).Value = 0;
                    }
                }
                else
                {
                    if (((SqlBinary)spv.SerializedValue).Length > Int32.MaxValue)
                    {
                        bTooBig = true;
                    }
                    else
                    {
                        if (searchOperator == SearchOperator.Contains || searchOperator == SearchOperator.FreeText)
                        {
                            // if the searchOperator is a freetext operator then pass the value unaltered to the
                            // @PropertyValueString because we are passing a string anyway not a binary
                            FindProfilesCommand.Parameters.Add("@PropertyValueString",
                                System.Data.SqlDbType.NVarChar, Int32.MaxValue).Value = spv.PropertyValue;
                            FindProfilesCommand.Parameters.Add("@PropertyValueBinary",
                                System.Data.SqlDbType.VarBinary, Int32.MaxValue).Value = DBNull.Value;
                        }
                        else
                        {
                            // otherwise just serialise the value and pass it to the @PropertyBinaryValue
                            FindProfilesCommand.Parameters.Add("@PropertyValueString",
                                System.Data.SqlDbType.NVarChar, Int32.MaxValue).Value = DBNull.Value;
                            FindProfilesCommand.Parameters.Add("@PropertyValueBinary",
                                System.Data.SqlDbType.VarBinary, Int32.MaxValue).Value = spv.SerializedValue;
                        }

                        // set the parameter @PropertyType to indicate the property is a binary
                        FindProfilesCommand.Parameters.Add("@PropertyType", System.Data.SqlDbType.Bit).Value = 1;
                    }
                }

                if (bTooBig)
                {
                    // if the size is out of limits throw an exception
                    throw new ProviderException("Property value length is too big.");
                }

                // Open the database
                conn.Open();

                // Get a DataReader for the results we need
                using (SqlDataReader rdr = FindProfilesCommand.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    while (rdr.Read())
                    {
                        // create a ProfileInfo with the data you got back from the current record of the SqlDataReader
                        ProfileInfo pi = new ProfileInfo(rdr.GetString(rdr.GetOrdinal("UserName")),
                            rdr.GetBoolean(rdr.GetOrdinal("IsAnonymous")),
                            DateTime.SpecifyKind(rdr.GetDateTime(rdr.GetOrdinal("LastActivityDate")), DateTimeKind.Utc),
                            DateTime.SpecifyKind(rdr.GetDateTime(rdr.GetOrdinal("LastUpdatedDate")), DateTimeKind.Utc), 0);

                        // add the ProfileInfo you just created to the ProfileInfoCollection that we will return
                        pic.Add(pi);
                    }
                }
            }
        }
        catch (Exception e)
        {
            // if anything went wrong, throw an exception
            throw new ProviderException("An error occured while finding profiles with your search criteria!", e);
        }

        return pic;
    }
    #endregion

    #region Private Implementations
    private void GetNonDefaultPropertyValuesForUser(String username, SettingsPropertyValueCollection spvc)
    {
        try
        {
            // Get the connection we're going to use
            using (SqlConnection conn = new SqlConnection(_connectionStringName))
            {
                // set the command object to the stored procedure used to get the properties
                // set the command to use the connection
                // set the command timeout value
                // initialize the stored procedure parameters to their values
                SqlCommand GetPropertiesCommand = new SqlCommand("aspnet_Profile_GetProperties", conn);
                GetPropertiesCommand.CommandTimeout = _commandTimeout;
                GetPropertiesCommand.CommandType = CommandType.StoredProcedure;
                GetPropertiesCommand.Parameters.Add("@ApplicationName", SqlDbType.NVarChar, 256).Value = base.ApplicationName;
                GetPropertiesCommand.Parameters.Add("@UserName", SqlDbType.NVarChar, 256).Value = username;
                GetPropertiesCommand.Parameters.Add("@CurrentTimeUtc", SqlDbType.DateTime).Value = DateTime.UtcNow;

                // open the connection
                conn.Open();

                // using a DataReader populated with the stored procedure
                using (SqlDataReader dReader = GetPropertiesCommand.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    // while there is anything to read
                    while (dReader.Read())
                    {
                        // get the property value corresponding to the PropertyName field in the reader
                        SettingsPropertyValue spv = spvc[dReader.GetString(dReader.GetOrdinal("PropertyName"))];

                        // if there is such a profile property
                        if (spv != null)
                        {
                            // get its value from the reader
                            GetPropertyValueFromReader(spv, dReader);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            // if anything went wrong, throw an exception to be handled by the calling function
            throw e;
        }
    }

    private Boolean GetPropertyValueFromReader(SettingsPropertyValue spv, SqlDataReader rdr)
    {
        try
        {
            // get the ordinals of value columns, just a speed optimization issue
            Int32 PropertyValueStringOrdinal = rdr.GetOrdinal("PropertyValueString");
            Int32 PropertyValueBinaryOrdinal = rdr.GetOrdinal("PropertyValueBinary");

            // Get the value based on the SerializeAs value
            switch (spv.Property.SerializeAs)
            {
                case SettingsSerializeAs.String:
                    {
                        // If the value string is null, set property value to null
                        if (rdr.IsDBNull(PropertyValueStringOrdinal))
                        {
                            spv.PropertyValue = null;
                        }
                        else
                        {
                            // no deserialization needed, so set the PropertyValue the same as the database value, changing its type ofcourse
                            spv.PropertyValue = Convert.ChangeType(rdr.GetString(PropertyValueStringOrdinal), spv.Property.PropertyType);
                        }

                        spv.Deserialized = true;

                        break;
                    }

                case SettingsSerializeAs.Binary:
                    {
                        if (rdr.IsDBNull(PropertyValueBinaryOrdinal))
                        {
                            spv.PropertyValue = null;
                            spv.Deserialized = true;
                        }
                        else
                        {
                            spv.SerializedValue = rdr.GetSqlBinary(PropertyValueBinaryOrdinal).Value;
                            spv.Deserialized = false;
                        }

                        break;
                    }

                case SettingsSerializeAs.Xml:
                    {
                        if (rdr.IsDBNull(PropertyValueStringOrdinal))
                        {
                            spv.PropertyValue = null;
                            spv.Deserialized = true;
                        }
                        else
                        {
                            spv.SerializedValue = rdr.GetString(PropertyValueStringOrdinal);
                            spv.Deserialized = false;
                        }

                        break;
                    }
                default:
                    {
                        throw new ProviderException(
                            String.Format("Could not determine correct serialization format for profile property '{0}'.", spv.Name));
                    }
            }
        }
        catch (Exception e)
        {
            throw new ProviderException(String.Format("Error deserialising profile property '{0}'.", spv.Name), e);
        }

        // set is not dirty, we are just reading
        spv.IsDirty = false;

        return true;
    }
    #endregion
}