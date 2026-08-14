CREATE TABLE [ShiftTypes] (
    [ShiftTypeId] int NOT NULL IDENTITY,
    [ShiftCode] nvarchar(30) NOT NULL,
    [ShiftName] nvarchar(100) NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [DurationHours] decimal(4,2) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_ShiftTypes] PRIMARY KEY ([ShiftTypeId])
);
GO


CREATE TABLE [UserAccounts] (
    [UserAccountId] int NOT NULL IDENTITY,
    [Username] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [Role] int NOT NULL,
    [IsLocked] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedByAccountId] int NULL,
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([UserAccountId]),
    CONSTRAINT [FK_UserAccounts_UserAccounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AuditLogs] (
    [AuditLogId] bigint NOT NULL IDENTITY,
    [ActorAccountId] int NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [EntityName] nvarchar(100) NOT NULL,
    [EntityId] nvarchar(100) NOT NULL,
    [PreviousValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditLogId]),
    CONSTRAINT [FK_AuditLogs_UserAccounts_ActorAccountId] FOREIGN KEY ([ActorAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Employees] (
    [EmployeeId] int NOT NULL IDENTITY,
    [UserAccountId] int NULL,
    [EmployeeCode] nvarchar(30) NOT NULL,
    [FullName] nvarchar(150) NOT NULL,
    [DateOfBirth] date NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [Email] nvarchar(150) NULL,
    [Position] int NOT NULL,
    [EmploymentType] int NOT NULL,
    [Level] int NOT NULL,
    [StartDate] date NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([EmployeeId]),
    CONSTRAINT [FK_Employees_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Notifications] (
    [NotificationId] int NOT NULL IDENTITY,
    [ReceiverAccountId] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [Type] int NOT NULL,
    [RelatedObjectId] int NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ReadAt] datetime2 NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
    CONSTRAINT [FK_Notifications_UserAccounts_ReceiverAccountId] FOREIGN KEY ([ReceiverAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [SchedulePeriods] (
    [SchedulePeriodId] int NOT NULL IDENTITY,
    [PeriodName] nvarchar(150) NOT NULL,
    [WeekStartDate] date NOT NULL,
    [WeekEndDate] date NOT NULL,
    [AvailabilityDeadline] datetime2 NOT NULL,
    [RegistrationStatus] int NOT NULL,
    [CreatedByAccountId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SchedulePeriods] PRIMARY KEY ([SchedulePeriodId]),
    CONSTRAINT [FK_SchedulePeriods_UserAccounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Availabilities] (
    [AvailabilityId] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [SchedulePeriodId] int NOT NULL,
    [WorkDate] date NOT NULL,
    [ShiftTypeId] int NOT NULL,
    [RegisteredAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Availabilities] PRIMARY KEY ([AvailabilityId]),
    CONSTRAINT [FK_Availabilities_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Availabilities_SchedulePeriods_SchedulePeriodId] FOREIGN KEY ([SchedulePeriodId]) REFERENCES [SchedulePeriods] ([SchedulePeriodId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Availabilities_ShiftTypes_ShiftTypeId] FOREIGN KEY ([ShiftTypeId]) REFERENCES [ShiftTypes] ([ShiftTypeId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LeaveRequests] (
    [LeaveRequestId] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [SchedulePeriodId] int NOT NULL,
    [LeaveDate] date NOT NULL,
    [ShiftTypeId] int NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [Status] int NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    [ApprovedByAccountId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ManagerNote] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([LeaveRequestId]),
    CONSTRAINT [FK_LeaveRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_SchedulePeriods_SchedulePeriodId] FOREIGN KEY ([SchedulePeriodId]) REFERENCES [SchedulePeriods] ([SchedulePeriodId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_ShiftTypes_ShiftTypeId] FOREIGN KEY ([ShiftTypeId]) REFERENCES [ShiftTypes] ([ShiftTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_UserAccounts_ApprovedByAccountId] FOREIGN KEY ([ApprovedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [StaffingRequirements] (
    [StaffingRequirementId] int NOT NULL IDENTITY,
    [SchedulePeriodId] int NOT NULL,
    [WorkDate] date NOT NULL,
    [ShiftTypeId] int NOT NULL,
    [RequiredSalesStaff] int NOT NULL,
    [RequiredCashiers] int NOT NULL,
    [MinimumLevel] int NOT NULL,
    [RequiredExperiencedStaff] int NOT NULL,
    CONSTRAINT [PK_StaffingRequirements] PRIMARY KEY ([StaffingRequirementId]),
    CONSTRAINT [FK_StaffingRequirements_SchedulePeriods_SchedulePeriodId] FOREIGN KEY ([SchedulePeriodId]) REFERENCES [SchedulePeriods] ([SchedulePeriodId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffingRequirements_ShiftTypes_ShiftTypeId] FOREIGN KEY ([ShiftTypeId]) REFERENCES [ShiftTypes] ([ShiftTypeId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkSchedules] (
    [WorkScheduleId] int NOT NULL IDENTITY,
    [SchedulePeriodId] int NOT NULL,
    [ScheduleName] nvarchar(150) NOT NULL,
    [Status] int NOT NULL,
    [IsGeneratedByORTools] bit NOT NULL,
    [CreatedByAccountId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [PublishedByAccountId] int NULL,
    [PublishedAt] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_WorkSchedules] PRIMARY KEY ([WorkScheduleId]),
    CONSTRAINT [FK_WorkSchedules_SchedulePeriods_SchedulePeriodId] FOREIGN KEY ([SchedulePeriodId]) REFERENCES [SchedulePeriods] ([SchedulePeriodId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSchedules_UserAccounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSchedules_UserAccounts_PublishedByAccountId] FOREIGN KEY ([PublishedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ShiftAssignments] (
    [ShiftAssignmentId] int NOT NULL IDENTITY,
    [WorkScheduleId] int NOT NULL,
    [EmployeeId] int NOT NULL,
    [WorkDate] date NOT NULL,
    [ShiftTypeId] int NOT NULL,
    [Source] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([ShiftAssignmentId]),
    CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftAssignments_ShiftTypes_ShiftTypeId] FOREIGN KEY ([ShiftTypeId]) REFERENCES [ShiftTypes] ([ShiftTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftAssignments_WorkSchedules_WorkScheduleId] FOREIGN KEY ([WorkScheduleId]) REFERENCES [WorkSchedules] ([WorkScheduleId]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ShiftTransferRequests] (
    [ShiftTransferRequestId] int NOT NULL IDENTITY,
    [ShiftAssignmentId] int NOT NULL,
    [SenderEmployeeId] int NOT NULL,
    [ReceiverEmployeeId] int NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ReceiverStatus] int NOT NULL,
    [ApprovalStatus] int NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    [ReceiverRespondedAt] datetime2 NULL,
    [ApprovedByAccountId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ManagerNote] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ShiftTransferRequests] PRIMARY KEY ([ShiftTransferRequestId]),
    CONSTRAINT [FK_ShiftTransferRequests_Employees_ReceiverEmployeeId] FOREIGN KEY ([ReceiverEmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftTransferRequests_Employees_SenderEmployeeId] FOREIGN KEY ([SenderEmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftTransferRequests_ShiftAssignments_ShiftAssignmentId] FOREIGN KEY ([ShiftAssignmentId]) REFERENCES [ShiftAssignments] ([ShiftAssignmentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftTransferRequests_UserAccounts_ApprovedByAccountId] FOREIGN KEY ([ApprovedByAccountId]) REFERENCES [UserAccounts] ([UserAccountId]) ON DELETE NO ACTION
);
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ShiftTypeId', N'DisplayOrder', N'DurationHours', N'EndTime', N'IsActive', N'ShiftCode', N'ShiftName', N'StartTime') AND [object_id] = OBJECT_ID(N'[ShiftTypes]'))
    SET IDENTITY_INSERT [ShiftTypes] ON;
INSERT INTO [ShiftTypes] ([ShiftTypeId], [DisplayOrder], [DurationHours], [EndTime], [IsActive], [ShiftCode], [ShiftName], [StartTime])
VALUES (1, 1, 4.0, '12:00:00', CAST(1 AS bit), N'MORNING', N'Ca sáng', '08:00:00'),
(2, 2, 4.0, '16:00:00', CAST(1 AS bit), N'AFTERNOON', N'Ca chiều', '12:00:00'),
(3, 3, 4.0, '20:00:00', CAST(1 AS bit), N'EVENING', N'Ca tối', '16:00:00');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ShiftTypeId', N'DisplayOrder', N'DurationHours', N'EndTime', N'IsActive', N'ShiftCode', N'ShiftName', N'StartTime') AND [object_id] = OBJECT_ID(N'[ShiftTypes]'))
    SET IDENTITY_INSERT [ShiftTypes] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'UserAccountId', N'CreatedAt', N'CreatedByAccountId', N'IsLocked', N'PasswordHash', N'Role', N'Username') AND [object_id] = OBJECT_ID(N'[UserAccounts]'))
    SET IDENTITY_INSERT [UserAccounts] ON;
INSERT INTO [UserAccounts] ([UserAccountId], [CreatedAt], [CreatedByAccountId], [IsLocked], [PasswordHash], [Role], [Username])
VALUES (1, '2026-08-01T00:00:00.0000000Z', NULL, CAST(0 AS bit), N'100000.ABEiM0RVZneImaq7zN3u/w==.voGHdh8BUEr7qGOgntpkfAlsfKyMx/CzNR/DLboeAU4=', 1, N'admin'),
(2, '2026-08-01T00:00:00.0000000Z', 1, CAST(0 AS bit), N'100000./+7dzLuqmYh3ZlVEMyIRAA==.UWV+wqzCmlDi0tlbWmy/DpHdbaaFtpczTXzp5MErAog=', 2, N'quanly');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'UserAccountId', N'CreatedAt', N'CreatedByAccountId', N'IsLocked', N'PasswordHash', N'Role', N'Username') AND [object_id] = OBJECT_ID(N'[UserAccounts]'))
    SET IDENTITY_INSERT [UserAccounts] OFF;
GO


CREATE INDEX [IX_AuditLogs_ActorAccountId_CreatedAtUtc] ON [AuditLogs] ([ActorAccountId], [CreatedAtUtc]);
GO


CREATE INDEX [IX_AuditLogs_EntityName_EntityId_CreatedAtUtc] ON [AuditLogs] ([EntityName], [EntityId], [CreatedAtUtc]);
GO


CREATE UNIQUE INDEX [IX_Availabilities_EmployeeId_SchedulePeriodId_WorkDate_ShiftTypeId] ON [Availabilities] ([EmployeeId], [SchedulePeriodId], [WorkDate], [ShiftTypeId]);
GO


CREATE INDEX [IX_Availabilities_SchedulePeriodId] ON [Availabilities] ([SchedulePeriodId]);
GO


CREATE INDEX [IX_Availabilities_ShiftTypeId] ON [Availabilities] ([ShiftTypeId]);
GO


CREATE UNIQUE INDEX [IX_Employees_Email] ON [Employees] ([Email]) WHERE [Email] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Employees_EmployeeCode] ON [Employees] ([EmployeeCode]);
GO


CREATE UNIQUE INDEX [IX_Employees_UserAccountId] ON [Employees] ([UserAccountId]) WHERE [UserAccountId] IS NOT NULL;
GO


CREATE INDEX [IX_LeaveRequests_ApprovedByAccountId] ON [LeaveRequests] ([ApprovedByAccountId]);
GO


CREATE INDEX [IX_LeaveRequests_EmployeeId_SchedulePeriodId_LeaveDate_ShiftTypeId] ON [LeaveRequests] ([EmployeeId], [SchedulePeriodId], [LeaveDate], [ShiftTypeId]);
GO


CREATE INDEX [IX_LeaveRequests_SchedulePeriodId] ON [LeaveRequests] ([SchedulePeriodId]);
GO


CREATE INDEX [IX_LeaveRequests_ShiftTypeId] ON [LeaveRequests] ([ShiftTypeId]);
GO


CREATE INDEX [IX_Notifications_ReceiverAccountId_IsRead_CreatedAt] ON [Notifications] ([ReceiverAccountId], [IsRead], [CreatedAt]);
GO


CREATE INDEX [IX_SchedulePeriods_CreatedByAccountId] ON [SchedulePeriods] ([CreatedByAccountId]);
GO


CREATE UNIQUE INDEX [IX_SchedulePeriods_WeekStartDate] ON [SchedulePeriods] ([WeekStartDate]);
GO


CREATE INDEX [IX_ShiftAssignments_EmployeeId] ON [ShiftAssignments] ([EmployeeId]);
GO


CREATE INDEX [IX_ShiftAssignments_ShiftTypeId] ON [ShiftAssignments] ([ShiftTypeId]);
GO


CREATE UNIQUE INDEX [IX_ShiftAssignments_WorkScheduleId_EmployeeId_WorkDate_ShiftTypeId] ON [ShiftAssignments] ([WorkScheduleId], [EmployeeId], [WorkDate], [ShiftTypeId]);
GO


CREATE INDEX [IX_ShiftTransferRequests_ApprovedByAccountId] ON [ShiftTransferRequests] ([ApprovedByAccountId]);
GO


CREATE INDEX [IX_ShiftTransferRequests_ReceiverEmployeeId] ON [ShiftTransferRequests] ([ReceiverEmployeeId]);
GO


CREATE INDEX [IX_ShiftTransferRequests_SenderEmployeeId] ON [ShiftTransferRequests] ([SenderEmployeeId]);
GO


CREATE INDEX [IX_ShiftTransferRequests_ShiftAssignmentId] ON [ShiftTransferRequests] ([ShiftAssignmentId]);
GO


CREATE UNIQUE INDEX [IX_ShiftTypes_DisplayOrder] ON [ShiftTypes] ([DisplayOrder]);
GO


CREATE UNIQUE INDEX [IX_ShiftTypes_ShiftCode] ON [ShiftTypes] ([ShiftCode]);
GO


CREATE UNIQUE INDEX [IX_StaffingRequirements_SchedulePeriodId_WorkDate_ShiftTypeId] ON [StaffingRequirements] ([SchedulePeriodId], [WorkDate], [ShiftTypeId]);
GO


CREATE INDEX [IX_StaffingRequirements_ShiftTypeId] ON [StaffingRequirements] ([ShiftTypeId]);
GO


CREATE INDEX [IX_UserAccounts_CreatedByAccountId] ON [UserAccounts] ([CreatedByAccountId]);
GO


CREATE UNIQUE INDEX [IX_UserAccounts_Username] ON [UserAccounts] ([Username]);
GO


CREATE INDEX [IX_WorkSchedules_CreatedByAccountId] ON [WorkSchedules] ([CreatedByAccountId]);
GO


CREATE INDEX [IX_WorkSchedules_PublishedByAccountId] ON [WorkSchedules] ([PublishedByAccountId]);
GO


CREATE INDEX [IX_WorkSchedules_SchedulePeriodId] ON [WorkSchedules] ([SchedulePeriodId]);
GO


