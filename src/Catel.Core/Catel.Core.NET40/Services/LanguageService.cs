﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LanguageService.cs" company="Catel development team">
//   Copyright (c) 2008 - 2014 Catel development team. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Catel.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Catel.Caching;
    using Catel.Logging;
    using Catel.Reflection;
    using Catel.Services.Models;

#if NETFX_CORE
    using Windows.ApplicationModel.Resources;
#else
    using System.Resources;
#endif

    /// <summary>
    /// Service to implement the retrieval of language services.
    /// </summary>
    public class LanguageService : ILanguageService
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly List<ILanguageSource> _languageSources = new List<ILanguageSource>();

        private readonly ICacheStorage<LanguageResourceKey, string> _stringCache = new CacheStorage<LanguageResourceKey, string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageService"/> class.
        /// </summary>
        public LanguageService()
        {
            _languageSources.Add(new LanguageResourceSource("Catel.Core", "Catel.Properties", "Resources"));
            _languageSources.Add(new LanguageResourceSource("Catel.Core", "Catel.Properties", "Exceptions"));

            _languageSources.Add(new LanguageResourceSource("Catel.MVVM", "Catel.Properties", "Resources"));
            _languageSources.Add(new LanguageResourceSource("Catel.MVVM", "Catel.Properties", "Exceptions"));

            _languageSources.Add(new LanguageResourceSource("Catel.Extensions.Controls", "Catel.Properties", "Resources"));
            _languageSources.Add(new LanguageResourceSource("Catel.Extensions.Controls", "Catel.Properties", "Exceptions"));

            FallbackCulture = CultureInfo.CurrentUICulture;
            PreferredCulture = CultureInfo.CurrentUICulture;
        }

        #region Properties
        /// <summary>
        /// Gets or sets the fallback culture.
        /// </summary>
        /// <value>The fallback culture.</value>
        public CultureInfo FallbackCulture { get; set; }

        /// <summary>
        /// Gets or sets the preferred culture.
        /// </summary>
        /// <value>The preferred culture.</value>
        public CultureInfo PreferredCulture { get; set; }
        #endregion

        /// <summary>
        /// Registers the language source.
        /// </summary>
        /// <param name="languageSource">The language source.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="languageSource" /> is <c>null</c> or whitespace.</exception>
        public void RegisterLanguageSource(ILanguageSource languageSource)
        {
            Argument.IsNotNull(() => languageSource);

            _languageSources.Insert(0, languageSource);
        }

        /// <summary>
        /// Gets the string with the <see cref="PreferredCulture" />. If the preferred language cannot be
        /// found, this method will use the <see cref="FallbackCulture" /> to retrieve the
        /// string.
        /// </summary>
        /// <param name="resourceName">Name of the resource.</param>
        /// <returns>The string or <c>null</c> if the resource cannot be found.</returns>
        /// <exception cref="ArgumentException">The <paramref name="resourceName" /> is <c>null</c>.</exception>
        public string GetString(string resourceName)
        {
            var preferredString = GetString(resourceName, PreferredCulture);
            if (string.IsNullOrWhiteSpace(preferredString))
            {
                preferredString = GetString(resourceName, FallbackCulture);
            }

            return preferredString;
        }

        /// <summary>
        /// Gets the string with the specified culture.
        /// </summary>
        /// <param name="resourceName">Name of the resource.</param>
        /// <param name="cultureInfo">The culture information.</param>
        /// <returns>The string or <c>null</c> if the resource cannot be found.</returns>
        /// <exception cref="ArgumentException">The <paramref name="resourceName" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">The <paramref name="cultureInfo" /> is <c>null</c>.</exception>
        public string GetString(string resourceName, CultureInfo cultureInfo)
        {
            Argument.IsNotNullOrWhitespace("resourceName", resourceName);
            Argument.IsNotNull("cultureInfo", cultureInfo);

            var resourceKey = new LanguageResourceKey(resourceName, cultureInfo);
            return _stringCache.GetFromCacheOrFetch(resourceKey, () =>
            {
                foreach (var resourceFile in _languageSources)
                {
                    try
                    {
                        var value = GetString(resourceFile, resourceName, cultureInfo);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to get string for resource name '{0}' from resource file '{1}'", resourceName, resourceFile);
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Gets the string from the specified resource file with the current culture.
        /// </summary>
        /// <param name="languageSource">The language source.</param>
        /// <param name="resourceName">Name of the resource.</param>
        /// <param name="cultureInfo">The culture information.</param>
        /// <returns>The string or <c>null</c> if the string cannot be found.</returns>
        protected virtual string GetString(ILanguageSource languageSource, string resourceName, CultureInfo cultureInfo)
        {
            var source = languageSource.GetSource();

#if NETFX_CORE && !WIN81
            var resourceLoader = new ResourceLoader(source);
#elif WIN81
            var resourceLoader = ResourceLoader.GetForCurrentView(source);
#else
            var splittedString = source.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries);

            var assemblyName = splittedString[1].Trim();
            var containingAssemblyName = string.Format("{0},", assemblyName);
            var assembly = AssemblyHelper.GetLoadedAssemblies().FirstOrDefault(x => x.FullName.Contains(containingAssemblyName));
            if (assembly == null)
            {
                return null;
            }

            string resourceFile = splittedString[0];
            var resourceLoader = new ResourceManager(resourceFile, assembly);
#endif

#if NETFX_CORE
            return resourceLoader.GetString(resourceName);
#else
            return resourceLoader.GetString(resourceName, cultureInfo);
#endif
        }
    }
}