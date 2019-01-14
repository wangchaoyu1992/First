protected virtual void BuildContext()
        {
            Enforce.ArgumentNotNull(m_Context, "m_Context", "fields are not initialized");
            Enforce.ArgumentNotNull(m_Context.CheckParam, "m_Parameter", "fields are not initialized");
            var properties = new Dictionary<string, string>() {
                { "Configuration","Debug"},
                { "Platform", "AnyCPU" },
                //{"CheckForSystemRuntimeDependency", "true" },
                //{ "DesignTimeBuild", "true" },
                { "BuildingInsideVisualStudio", "true" }
            };
            MSBuildWorkspace workspace = MSBuildWorkspace.Create(properties);
            workspace.WorkspaceFailed += Workspace_WorkspaceFailed;
            Solution solution = workspace.OpenSolutionAsync(m_Context.CheckParam.SlnFullPath).Result;
            //List<string> projectNames = new List<string>();
            Dictionary<string, MsBuild.Project> msProjects = new Dictionary<string, MsBuild.Project>();

            for (int i = 0; i < solution.Projects.Count(); i++)
            {
                var project = solution.Projects.Skip(i).First();
                MsBuild.Project msProject = null;
                if (msProjects.Keys.Contains(project.Name))
                    msProject = msProjects[project.Name];
                else
                {
                    try {
                        msProject = new MsBuild.Project(project.FilePath, new Dictionary<string, string>() { { "BuildingInsideVisualStudio", "true" } }
                        , null/*, MsBuild.ProjectCollection.GlobalProjectCollection, MsBuild.ProjectLoadSettings.IgnoreMissingImports*/);
                    } catch (Exception e) {
                        logger.Error(e);
                        continue;
                    }
                    msProjects.Add(project.Name, msProject);
                }
                if (m_Context.CheckParam.Libs != null && m_Context.CheckParam.Libs.Count > 0)
                {
                    AddLibs(ref project, msProject);
                }
               
                BuildConfig(msProject);
                //转换页面文件为C#代码
                BuildView(ref project, msProject);

                foreach (var mr in project.MetadataReferences)
                {
                    DependenceLibsResult.Add(Common.GetRelativePath(m_Context.CheckParam.SourcePath, mr.Display));
                }
                foreach (Document document in project.Documents)
                {
                    SyntaxTree tree = document.GetSyntaxTreeAsync().Result;
                    SemanticModel model = document.GetSemanticModelAsync().Result;

                    if (!tree.FilePath.StartsWith(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files"))
                    {
                        foreach (var diag in model.GetDiagnostics().Where(t => t.Severity == DiagnosticSeverity.Error))
                        {
                            //compile errors 
							//Predefined type 'System.*' is not defined or imported
                            errors.Add(new Tuple<string, string>(SyntaxNodeHelper.GetLocationInfo(diag.Location), 
                                System.Security.SecurityElement.Escape(diag.GetMessage())));
                        }
                    }
                    SyntaxTreeTuple tuple = new SyntaxTreeTuple()
                    {
                        DocumentItem = document,
                        ProjectItem = project,
                        SemanticModelItem = model
                    };
                    m_Context.CheckSyntaxTreeContainer[tree] = tuple;
                }
                solution = project.Solution;
            }
            
        }