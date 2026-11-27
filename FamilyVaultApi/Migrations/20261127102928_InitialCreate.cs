using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_purposes",
                columns: table => new
                {
                    category_purpose_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_purposes", x => x.category_purpose_id);
                });

            migrationBuilder.CreateTable(
                name: "tb_roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_user",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    age = table.Column<int>(type: "int", nullable: false),
                    registered_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_login = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "tb_verification_codes",
                columns: table => new
                {
                    destination = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    purpose = table.Column<int>(type: "int", nullable: false),
                    code_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    failed_attempts = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_verification_codes", x => new { x.destination, x.purpose });
                });

            migrationBuilder.CreateTable(
                name: "tb_verification_send_limits",
                columns: table => new
                {
                    destination = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    day_window_started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    day_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_verification_send_limits", x => x.destination);
                });

            migrationBuilder.CreateTable(
                name: "transaction_types",
                columns: table => new
                {
                    transaction_type_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_types", x => x.transaction_type_id);
                });

            migrationBuilder.CreateTable(
                name: "tb_role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_role_claims_tb_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "tb_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    category_purpose_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    user_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.category_id);
                    table.ForeignKey(
                        name: "fk_categories_purpose",
                        column: x => x.category_purpose_id,
                        principalTable: "category_purposes",
                        principalColumn: "category_purpose_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_categories_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gmail_connections",
                columns: table => new
                {
                    gmail_connection_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    google_email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    refresh_token_encrypted = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    connected_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    initial_scan_completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    synced_until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_scan_requested_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_connections", x => x.gmail_connection_id);
                    table.ForeignKey(
                        name: "fk_gmail_connections_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gmail_ignored_senders",
                columns: table => new
                {
                    gmail_ignored_sender_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    sender = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_ignored_senders", x => x.gmail_ignored_sender_id);
                    table.ForeignKey(
                        name: "fk_gmail_ignored_senders_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gmail_oauth_states",
                columns: table => new
                {
                    gmail_oauth_state_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    state_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    return_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_oauth_states", x => x.gmail_oauth_state_id);
                    table.ForeignKey(
                        name: "fk_gmail_oauth_states_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gmail_scans",
                columns: table => new
                {
                    gmail_scan_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    window_start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    window_end = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    found = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    processed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_scans", x => x.gmail_scan_id);
                    table.ForeignKey(
                        name: "fk_gmail_scans_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pdf_sender_passwords",
                columns: table => new
                {
                    pdf_sender_password_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    sender = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    password_encrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_sender_passwords", x => x.pdf_sender_password_id);
                    table.ForeignKey(
                        name: "fk_pdf_sender_passwords_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personal_access_tokens",
                columns: table => new
                {
                    personal_access_token_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    scope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_access_tokens", x => x.personal_access_token_id);
                    table.ForeignKey(
                        name: "fk_personal_access_tokens_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_refresh_tokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    client = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_refresh_tokens", x => x.refresh_token_id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_user_claims_tb_user_UserId",
                        column: x => x.UserId,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_tb_user_logins_tb_user_UserId",
                        column: x => x.UserId,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_user_roles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_tb_user_roles_tb_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "tb_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_user_roles_tb_user_UserId",
                        column: x => x.UserId,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_user_tokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_tb_user_tokens_tb_user_UserId",
                        column: x => x.UserId,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    transaction_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    category_id = table.Column<int>(type: "int", nullable: false),
                    transaction_type_id = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    transaction_date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(CURRENT_TIMESTAMP AS date)"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.transaction_id);
                    table.ForeignKey(
                        name: "fk_transactions_category",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_type",
                        column: x => x.transaction_type_id,
                        principalTable: "transaction_types",
                        principalColumn: "transaction_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inbound_documents",
                columns: table => new
                {
                    inbound_document_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    external_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    sender = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    failure_reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    temporary_content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    content_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    processing_started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    gmail_scan_id = table.Column<int>(type: "int", nullable: true),
                    password_attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    password_required_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_documents", x => x.inbound_document_id);
                    table.ForeignKey(
                        name: "fk_inbound_documents_gmail_scan",
                        column: x => x.gmail_scan_id,
                        principalTable: "gmail_scans",
                        principalColumn: "gmail_scan_id");
                    table.ForeignKey(
                        name: "fk_inbound_documents_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                columns: table => new
                {
                    charge_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    inbound_document_id = table.Column<int>(type: "int", nullable: false),
                    issuer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    due_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    barcode = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    suggested_category_id = table.Column<int>(type: "int", nullable: true),
                    possible_duplicate_transaction_id = table.Column<int>(type: "int", nullable: true),
                    possible_duplicate_charge_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    extraction_source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    transaction_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charges", x => x.charge_id);
                    table.ForeignKey(
                        name: "fk_charges_inbound_document",
                        column: x => x.inbound_document_id,
                        principalTable: "inbound_documents",
                        principalColumn: "inbound_document_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_charges_possible_duplicate_charge",
                        column: x => x.possible_duplicate_charge_id,
                        principalTable: "charges",
                        principalColumn: "charge_id");
                    table.ForeignKey(
                        name: "fk_charges_possible_duplicate_transaction",
                        column: x => x.possible_duplicate_transaction_id,
                        principalTable: "transactions",
                        principalColumn: "transaction_id");
                    table.ForeignKey(
                        name: "fk_charges_suggested_category",
                        column: x => x.suggested_category_id,
                        principalTable: "categories",
                        principalColumn: "category_id");
                    table.ForeignKey(
                        name: "fk_charges_transaction",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "transaction_id");
                    table.ForeignKey(
                        name: "fk_charges_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id");
                });

            migrationBuilder.InsertData(
                table: "category_purposes",
                columns: new[] { "category_purpose_id", "code", "created_at", "description", "is_active", "name" },
                values: new object[,]
                {
                    { 1, "expense", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Apenas para despesas", true, "Despesa" },
                    { 2, "income", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Apenas para receitas", true, "Receita" }
                });

            migrationBuilder.InsertData(
                table: "tb_roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "5d92e12f-f00c-4899-ac98-89ea76712171", "8f3c1a52-6b7e-4d19-9a40-2c5e7d1b3f68", "Administrator", "ADMINISTRATOR" },
                    { "bad15740-437c-4721-839c-a9af6fbd3961", "e2b94d07-1c58-4a63-b7f1-5d8a0c3e9246", "User", "USER" }
                });

            migrationBuilder.InsertData(
                table: "transaction_types",
                columns: new[] { "transaction_type_id", "code", "created_at", "description", "is_active", "name" },
                values: new object[,]
                {
                    { 1, "expense", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Saída de recursos", true, "Despesa" },
                    { 2, "income", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Entrada de recursos", true, "Receita" }
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "category_id", "category_purpose_id", "created_at", "description", "user_id" },
                values: new object[,]
                {
                    { 1, 2, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Salário", null },
                    { 2, 1, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Alimentação", null },
                    { 3, 1, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Transporte", null },
                    { 4, 1, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Aporte em Investimentos", null },
                    { 5, 2, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Rendimento em Investimentos", null },
                    { 6, 1, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Lazer", null }
                });

            migrationBuilder.InsertData(
                table: "tb_role_claims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "RoleId" },
                values: new object[,]
                {
                    { 1, "permission", "ManageCategories", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 2, "permission", "ManageTransactionTypes", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 3, "permission", "ManageTransactions", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 4, "permission", "ManageUsers", "5d92e12f-f00c-4899-ac98-89ea76712171" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_categories_description",
                table: "categories",
                column: "description");

            migrationBuilder.CreateIndex(
                name: "idx_categories_purpose",
                table: "categories",
                column: "category_purpose_id");

            migrationBuilder.CreateIndex(
                name: "idx_categories_user",
                table: "categories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_category_purposes_active",
                table: "category_purposes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "uk_category_purposes_code",
                table: "category_purposes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_charges_inbound_document",
                table: "charges",
                column: "inbound_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_charges_possible_duplicate_charge",
                table: "charges",
                column: "possible_duplicate_charge_id");

            migrationBuilder.CreateIndex(
                name: "idx_charges_user_barcode",
                table: "charges",
                columns: new[] { "user_id", "barcode" });

            migrationBuilder.CreateIndex(
                name: "idx_charges_user_status",
                table: "charges",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_charges_possible_duplicate_transaction_id",
                table: "charges",
                column: "possible_duplicate_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_charges_suggested_category_id",
                table: "charges",
                column: "suggested_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_charges_transaction_id",
                table: "charges",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_connections_user",
                table: "gmail_connections",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gmail_ignored_senders_user_sender",
                table: "gmail_ignored_senders",
                columns: new[] { "user_id", "sender" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gmail_oauth_states_expires",
                table: "gmail_oauth_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_oauth_states_hash",
                table: "gmail_oauth_states",
                column: "state_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gmail_oauth_states_user_id",
                table: "gmail_oauth_states",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_scans_status_created",
                table: "gmail_scans",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_gmail_scans_user_status",
                table: "gmail_scans",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_inbound_documents_status_created",
                table: "inbound_documents",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_inbound_documents_user_external",
                table: "inbound_documents",
                columns: new[] { "user_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_documents_gmail_scan_id",
                table: "inbound_documents",
                column: "gmail_scan_id");

            migrationBuilder.CreateIndex(
                name: "idx_pdf_sender_passwords_user_sender",
                table: "pdf_sender_passwords",
                columns: new[] { "user_id", "sender" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_personal_access_tokens_user",
                table: "personal_access_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_personal_access_tokens_hash",
                table: "personal_access_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_family",
                table: "tb_refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_user",
                table: "tb_refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_tokens_hash",
                table: "tb_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tb_role_claims_RoleId",
                table: "tb_role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "tb_roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "tb_user",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "idx_users_age",
                table: "tb_user",
                column: "age");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "tb_user",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tb_user_claims_UserId",
                table: "tb_user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_user_logins_UserId",
                table: "tb_user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_user_roles_RoleId",
                table: "tb_user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "idx_verification_codes_expires",
                table: "tb_verification_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_verification_send_limits_window",
                table: "tb_verification_send_limits",
                column: "day_window_started_at");

            migrationBuilder.CreateIndex(
                name: "idx_transaction_types_active",
                table: "transaction_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "uk_transaction_types_code",
                table: "transaction_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_date",
                table: "transactions",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_type_id",
                table: "transactions",
                column: "transaction_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_user_id",
                table: "transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_transaction_date",
                table: "transactions",
                columns: new[] { "user_id", "transaction_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charges");

            migrationBuilder.DropTable(
                name: "gmail_connections");

            migrationBuilder.DropTable(
                name: "gmail_ignored_senders");

            migrationBuilder.DropTable(
                name: "gmail_oauth_states");

            migrationBuilder.DropTable(
                name: "pdf_sender_passwords");

            migrationBuilder.DropTable(
                name: "personal_access_tokens");

            migrationBuilder.DropTable(
                name: "tb_refresh_tokens");

            migrationBuilder.DropTable(
                name: "tb_role_claims");

            migrationBuilder.DropTable(
                name: "tb_user_claims");

            migrationBuilder.DropTable(
                name: "tb_user_logins");

            migrationBuilder.DropTable(
                name: "tb_user_roles");

            migrationBuilder.DropTable(
                name: "tb_user_tokens");

            migrationBuilder.DropTable(
                name: "tb_verification_codes");

            migrationBuilder.DropTable(
                name: "tb_verification_send_limits");

            migrationBuilder.DropTable(
                name: "inbound_documents");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "tb_roles");

            migrationBuilder.DropTable(
                name: "gmail_scans");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "transaction_types");

            migrationBuilder.DropTable(
                name: "category_purposes");

            migrationBuilder.DropTable(
                name: "tb_user");
        }
    }
}
