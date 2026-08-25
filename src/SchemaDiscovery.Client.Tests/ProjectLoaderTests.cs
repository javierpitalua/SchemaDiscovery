using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchemaDiscovery.Client.Models;

namespace SchemaDiscovery.Client.Tests
{
    [TestClass]
    public class ProjectLoaderTests
    {
        private static string TestFilesRoot => FindTestFilesDirectory();

        [TestMethod]
        public void Load_WithValidInputDirectory_ReturnsAllSchemaObjects()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            var expectedTableCount = Directory.GetFiles(Path.Combine(TestFilesRoot, "tables"), "*.json").Length;
            var expectedViewCount = Directory.GetFiles(Path.Combine(TestFilesRoot, "views"), "*.json").Length;
            var expectedRoutineCount =
                Directory.GetFiles(Path.Combine(TestFilesRoot, "stored-procedures"), "*.json").Length +
                Directory.GetFiles(Path.Combine(TestFilesRoot, "functions"), "*.json").Length;

            Assert.AreEqual(expectedTableCount, project.Tables.Count);
            Assert.AreEqual(expectedViewCount, project.Views.Count);
            Assert.AreEqual(expectedRoutineCount, project.Routines.Count);
        }

        [TestMethod]
        public void Load_PopulatesTableSchemaFromFile()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            var usuario = project.Tables.Single(t => t.Name == "Usuario");

            Assert.AreEqual("dbo", usuario.Schema);
            Assert.AreEqual(SchemaObjectType.Table, usuario.ObjectType);
            Assert.AreEqual("dbo.Usuario", usuario.QualifiedName);
            Assert.AreEqual(17, usuario.Columns.Count);
            Assert.AreEqual(76, usuario.RowCountEstimate);
            CollectionAssert.AreEqual(new[] { "Id" }, usuario.PrimaryKeyColumns.ToArray());

            var idColumn = usuario.Columns.Single(c => c.Name == "Id");
            Assert.AreEqual("int", idColumn.DataType);
            Assert.IsTrue(idColumn.IsIdentity);
            Assert.IsTrue(idColumn.IsPrimaryKey);
        }

        [TestMethod]
        public void Load_PopulatesViewSchemaFromFile()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            var view = project.Views.Single(v => v.Name == "vMembresiaVigente");

            Assert.AreEqual("dbo", view.Schema);
            Assert.AreEqual(SchemaObjectType.View, view.ObjectType);
            Assert.AreEqual("dbo.vMembresiaVigente", view.QualifiedName);
            Assert.AreEqual(16, view.Columns.Count);
            StringAssert.Contains(view.Definition, "CREATE VIEW");
        }

        [TestMethod]
        public void Load_PopulatesStoredProcedureFromFile()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            var procedure = project.Routines.Single(r => r.Name == "get_Membresia_ObtenerPorSuscriptor");

            Assert.AreEqual(SchemaObjectType.StoredProcedure, procedure.ObjectType);
            Assert.AreEqual("dbo", procedure.Schema);
            Assert.IsNull(procedure.ReturnType);
            Assert.AreEqual(1, procedure.Parameters.Count);
            Assert.AreEqual("@Suscriptor_Id", procedure.Parameters[0].Name);
            Assert.AreEqual("int", procedure.Parameters[0].DataType);
            StringAssert.Contains(procedure.Definition, "CREATE PROCEDURE");
        }

        [TestMethod]
        public void Load_PopulatesFunctionFromFile()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            var function = project.Routines.Single(r => r.Name == "fnObtenerDescripcionMes");

            Assert.AreEqual(SchemaObjectType.Function, function.ObjectType);
            Assert.AreEqual("varchar", function.ReturnType);
            Assert.AreEqual(1, function.Parameters.Count);
            Assert.AreEqual("@mes", function.Parameters[0].Name);
            StringAssert.Contains(function.Definition, "CREATE FUNCTION");
        }

        [TestMethod]
        public void Load_PopulatesProjectInfoFromProjectInfoJson()
        {
            var project = new ProjectLoader().Load(TestFilesRoot);

            Assert.AreEqual("sqlserver", project.ProviderName);
            Assert.AreEqual(DateTimeOffset.Parse("2026-08-24T23:42:32.1725042+00:00"), project.ScannedAtUtc);
            Assert.AreEqual("Spanish", project.CultureLanguage);
        }

        [TestMethod]
        public void Load_WithoutProjectInfoJson_LeavesProjectInfoPropertiesAtDefaults()
        {
            var emptyDirectory = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(emptyDirectory);

            try
            {
                var project = new ProjectLoader().Load(emptyDirectory);

                Assert.IsNull(project.ProviderName);
                Assert.AreEqual(default(DateTimeOffset), project.ScannedAtUtc);
                Assert.IsNull(project.CultureLanguage);
            }
            finally
            {
                Directory.Delete(emptyDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void Load_WithMalformedProjectInfoJson_ThrowsJsonException()
        {
            var directory = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "projectInfo.json"), "{ not valid json");

            try
            {
                Exception thrown = null;
                try
                {
                    new ProjectLoader().Load(directory);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                Assert.IsInstanceOfType(thrown, typeof(JsonException));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void LoadProject_StaticHelper_ReturnsSameResultAsInstanceMethod()
        {
            var project = ProjectLoader.LoadProject(TestFilesRoot);

            Assert.IsTrue(project.Tables.Count > 0);
            Assert.IsTrue(project.Views.Count > 0);
            Assert.IsTrue(project.Routines.Count > 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Load_WithNullPath_ThrowsArgumentException()
        {
            new ProjectLoader().Load(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Load_WithWhitespacePath_ThrowsArgumentException()
        {
            new ProjectLoader().Load("   ");
        }

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public void Load_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());

            new ProjectLoader().Load(missingPath);
        }

        [TestMethod]
        public void Load_WithDirectoryMissingSchemaSubfolders_ReturnsEmptyProject()
        {
            var emptyDirectory = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(emptyDirectory);

            try
            {
                var project = new ProjectLoader().Load(emptyDirectory);

                Assert.AreEqual(0, project.Tables.Count);
                Assert.AreEqual(0, project.Views.Count);
                Assert.AreEqual(0, project.Routines.Count);
            }
            finally
            {
                Directory.Delete(emptyDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void Load_WithFileMissingObjectType_ThrowsInvalidDataException()
        {
            var directory = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());
            var tablesDirectory = Path.Combine(directory, "tables");
            Directory.CreateDirectory(tablesDirectory);
            File.WriteAllText(Path.Combine(tablesDirectory, "bad.json"), "{ \"Name\": \"NoType\" }");

            try
            {
                Assert.ThrowsException<InvalidDataException>(() => new ProjectLoader().Load(directory));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void Load_WithMalformedJson_ThrowsJsonException()
        {
            var directory = Path.Combine(Path.GetTempPath(), "schema-discovery-tests-" + Guid.NewGuid());
            var tablesDirectory = Path.Combine(directory, "tables");
            Directory.CreateDirectory(tablesDirectory);
            File.WriteAllText(Path.Combine(tablesDirectory, "bad.json"), "{ not valid json");

            try
            {
                Exception thrown = null;
                try
                {
                    new ProjectLoader().Load(directory);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                Assert.IsInstanceOfType(thrown, typeof(JsonException));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string FindTestFilesDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "test-files")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
                throw new DirectoryNotFoundException("Could not locate the 'test-files' directory relative to the test assembly.");

            return Path.Combine(directory.FullName, "test-files");
        }
    }
}
