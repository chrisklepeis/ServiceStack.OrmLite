﻿using System;
using System.Data;
using System.Linq;
using NUnit.Framework;
using ServiceStack.DataAnnotations;
using ServiceStack.Logging;
using ServiceStack.Text;

namespace ServiceStack.OrmLite.Tests.Issues
{
    public class AnyObjectClass
    {
        [Alias("_id")]
        public Guid Id { get; set; }

        [Alias("_identity")]
        public Guid? Identity { get; set; }

        [Alias("_name")]
        [StringLength(250)]
        public string Name { get; set; }
    }

    public class SqlExpressionSubSqlExpressionIssue : OrmLiteTestBase
    {
        private static void RecreateAnyObjectTables(IDbConnection db)
        {
            db.DropTable<AnyObjectClassItem>();
            db.DropTable<AnyObjectClass>();
            db.CreateTable<AnyObjectClass>();
            db.CreateTable<AnyObjectClassItem>();
        }

        [Test]
        public void Can_compare_null_constant_in_subquery()
        {
            LogManager.LogFactory = new ConsoleLogFactory();

            using (var db = OpenDbConnection())
            {
                RecreateAnyObjectTables(db);

                var inQ = db.From<AnyObjectClass>()
                    .Where(y => y.Identity != null)
                    .Select(y => y.Identity.Value);

                var q = db.From<AnyObjectClass>().Where(x => Sql.In(x.Identity, inQ));

                var results = db.Select(q);

                results.PrintDump();
            }
        }

        [Test]
        public void Can_compare_null_constant_in_subquery_nested_in_SqlExpression()
        {
            LogManager.LogFactory = new ConsoleLogFactory();

            using (var db = OpenDbConnection())
            {
                RecreateAnyObjectTables(db);

                var q = db.From<AnyObjectClass>().Where(x => Sql.In(x.Identity, 
                    db.From<AnyObjectClass>()
                        .Where(y => y.Identity != null)
                        .Select(y => y.Identity.Value)));

                var results = db.Select(q);

                results.PrintDump();
            }
        }

        public class Person2
        {
            [AutoIncrement]
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public class Order2
        {
            [AutoIncrement]
            public int Id { get; set; }

            [References(typeof(Person2))]
            public int Person2Id { get; set; }

            public DateTime OrderDate { get; set; }

            public int OrderTypeId { get; set; }
        }

        [Test]
        public void Can_reference_variable_in_sub_expression()
        {
            int orderTypeId = 2;

            using (var db = OpenDbConnection())
            {
                var subExpr = db.From<Order2>()
                    .Where(y => y.OrderTypeId == orderTypeId)
                    .Select(y => y.Person2Id);

                subExpr.ToSelectStatement().Print();
                Assert.That(subExpr.ToSelectStatement().NormalizeSql(), Is.StringContaining("@0"));

                var expr = db.From<Person2>()
                    .Where(x => Sql.In(x.Id, subExpr));

                expr.ToSelectStatement().Print();
                Assert.That(expr.ToSelectStatement().NormalizeSql(), Is.StringContaining("@0"));
            }
        }

        public class AnyObjectClass
        {
            public Guid? Identity { get; set; }

            public string Name { get; set; }

            public IDbConnection db;

            [DataAnnotations.Ignore]
            public decimal CustomProperty
            {
                get
                {
                    return db.Select<AnyObjectClassItem>
                        (s => Sql.In(s.Identity,
                            db.From<AnyObjectClassItem>()
                                .Where(b => b.AnyObjectClassId == this.Identity)
                                .Select(b => b.Identity))
                        ).Sum(r => r.PurchasePrice);
                }
            }
        }

        public class AnyObjectClassItem
        {
            public Guid? Identity { get; set; }

            public string Name { get; set; }

            public decimal PurchasePrice { get; set; }

            [ForeignKey(typeof(AnyObjectClass))]
            public Guid AnyObjectClassId { get; set; }

            public AnyObjectClass AnyObjectClass { get; set; }
        }

        [Test]
        public void Can_select_sub_expression_when_called_within_a_datamodel()
        {
            using (var db = OpenDbConnection())
            {
                RecreateAnyObjectTables(db);

                var model = new AnyObjectClass { db = db };
                var result = model.CustomProperty;

                db.GetLastSql().Print();

                result.PrintDump();
            }
        }
    }
}