﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(FileChangeTrackerFactory), ServiceLayer.Host)]
    internal class VisualStudioFileChangeTrackerFactoryFactory : IWorkspaceServiceFactory
    {
        private readonly IVsAsyncFileChangeEx _fileChangeService;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioFileChangeTrackerFactoryFactory(
            SVsServiceProvider serviceProvider,
            ForegroundDispatcher foregroundDispatcher,
            JoinableTaskContext joinableTaskContext)
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _fileChangeService = serviceProvider.GetService(typeof(SVsFileChangeEx)) as IVsAsyncFileChangeEx;
            _foregroundDispatcher = foregroundDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            if (workspaceServices == null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            var errorReporter = workspaceServices.GetRequiredService<ErrorReporter>();
            return new VisualStudioFileChangeTrackerFactory(errorReporter, _fileChangeService, _foregroundDispatcher, _joinableTaskContext);
        }
    }
}
