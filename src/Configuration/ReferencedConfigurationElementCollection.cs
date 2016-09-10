﻿#region GNU GPL Version 3 License

/// Copyright 2013 Shailesh Lolam
/// 
/// This file ReferencedConfigurationElementCollection.cs is part of XecMe.
/// 
/// XecMe is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
/// 
/// XecMe is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License along with XecMe. If not, see http://www.gnu.org/licenses/.
/// 
/// History:
/// ______________________________________________________________
/// Created         01-2013             Shailesh Lolam

#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Xml;
using XecMe.Common;

namespace XecMe.Configuration
{
    /// <summary>
    /// This class is used reference based element parsing. Custom define a tag using the extensions
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReferencedConfigurationElementCollection<T> : ConfigurationElementCollection where T : ConfigurationElement, new()
    {
        /// <summary>
        /// ExtensionElement collection field
        /// </summary>
        private ConfigurationElementCollection<ExtensionElement> _extensions;

        /// <summary>
        /// Extention elements collection used for parsing the collection
        /// </summary>
        public ConfigurationElementCollection<ExtensionElement> Extensions 
        { 
            get { return _extensions;}
            set { _extensions = value; }
        }

        /// <summary>
        /// When overridden in a derived class, creates a new <see cref="T:System.Configuration.ConfigurationElement" />.
        /// </summary>
        /// <returns>
        /// A newly created <see cref="T:System.Configuration.ConfigurationElement" />.
        /// </returns>
        protected override ConfigurationElement CreateNewElement()
        {
            ExtensionElement eElement = _extensions[this.AddElementName];
            Type elementType = Type.GetType(eElement.Type);
            return Reflection.CreateInstance<T>(elementType);
        }

        /// <summary>
        /// Gets the element key for a specified configuration element when overridden in a derived class.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Configuration.ConfigurationElement" /> to return the key for.</param>
        /// <returns>
        /// An <see cref="T:System.Object" /> that acts as the key for the specified <see cref="T:System.Configuration.ConfigurationElement" />.
        /// </returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            element.NotNull(nameof(element));

            foreach (PropertyInformation information in element.ElementInformation.Properties)
            {
                if (information.IsKey)
                {
                    return information.Value;
                }
            }
            return element.ToString();
        }

        /// <summary>
        /// Causes the configuration system to throw an exception.
        /// </summary>
        /// <param name="elementName">The name of the unrecognized element.</param>
        /// <param name="reader">An input stream that reads XML from the configuration file.</param>
        /// <returns>
        /// true if the unrecognized element was deserialized successfully; otherwise, false. The default is false.
        /// </returns>
        /// <exception cref="InvalidOperationException">Extensions are not referenced to this collection.</exception>
        /// <exception cref="ConfigurationErrorsException">
        /// </exception>
        protected override bool OnDeserializeUnrecognizedElement(string elementName, System.Xml.XmlReader reader)
        {
            if (_extensions == null)
                throw new InvalidOperationException("Extensions are not referenced to this collection.");

            ExtensionElement eElement = _extensions[elementName];
            Type elementType = Type.GetType(eElement.Type);
            if (elementType == null)
                throw new ConfigurationErrorsException(string.Format("Cannot load type \"{0}\"", eElement.Type));
            if (elementType.IsSubclassOf(typeof(T)))
            {
                ///Change the AddElementName to use the default parsing
                this.AddElementName = elementName;
                return base.OnDeserializeUnrecognizedElement(elementName, reader);
            }
            else
            {
                throw new ConfigurationErrorsException(string.Format("Type \"{0}\" is not subclass of \"{1}\"",elementType.ToString(), typeof(T).ToString()));
            }
        }
    }
}
