namespace KobNeti.Api.Data;

/// <summary>Default platform help articles (internal staff docs at /admin/help).</summary>
public static class PlatformHelpSeed
{
    public sealed record Article(string Slug, string Title, string Body, string Category, int SortOrder);

    public static IReadOnlyList<Article> All { get; } =
    [
        new("getting-started", "Getting Started with KobNeti",
            """
            KobNeti is your operations workspace for support, engineering, people ops, and platform administration. Each product (brand) has its own data scope — switch products from the sidebar without signing out.

            1. Sign in:
            Go to /admin/login and authenticate with your staff account. In development you may use Quick sign-in when configured.

            2. Pick a product:
            Use the product switcher at the top of the sidebar. Tickets, engineering tasks, time entries, and files are always scoped to the active product.

            3. Navigate the sidebar:
            Support Hub handles customer-facing work. Engineering covers boards and GitHub activity. People Ops includes time tracking, approvals, and payroll. Platform covers calendar, files, audit, registry, assets, and this Help Center.

            4. Customer vs internal help:
            The public Knowledge Base (Support → Knowledge Base) is for customers. This Help Center (/admin/help) is for internal staff documentation only.
            """, "getting-started", 1),

        new("support-hub", "Support Hub Overview",
            """
            The Support Hub is your home for live chat, tickets, incidents, and the customer knowledge base.

            1. Open Support Hub:
            Navigate to /admin/support from the sidebar. You will see tabs for Live Chat, Tickets, Incidents, and Knowledge Base.

            2. Live Chat:
            Monitor active visitor conversations in real time. Claim a chat to respond, use canned replies, and link related tickets when needed.

            3. Tickets:
            View the ticket queue filtered by status and priority. Open a ticket to see conversation history, internal notes, SLA timers, and suggested KB articles.

            4. Incidents:
            Track product outages and major issues separately from individual tickets. Update status and communicate impact to the team.

            5. Knowledge Base (customer-facing):
            Create and publish articles that appear on the public help center. Draft articles stay hidden until published.
            """, "support", 2),

        new("live-chat-tickets", "Live Chat & Tickets",
            """
            This guide covers day-to-day ticket and chat workflows for support agents.

            1. Claim or assign:
            From the ticket list, open an unassigned ticket and assign it to yourself or a teammate. Live chats can be claimed from the active sessions panel.

            2. Reply vs internal note:
            Customer replies are visible to the requester. Toggle Internal Note when collaborating with other staff — customers never see internal notes.

            3. Link knowledge base articles:
            Use the KB quick panel to search published articles and share links in replies. Suggested articles may appear automatically based on ticket content.

            4. Resolve and close:
            Set status to Resolved when the issue is fixed. Add resolution tags if prompted. Closed tickets remain searchable for reporting.

            5. SLA awareness:
            Watch SLA badges on tickets. Escalate urgent items before breach when your team policy requires it.
            """, "support", 3),

        new("engineering-board", "Engineering Task Board",
            """
            Engineering tools help your team plan work, track milestones, and connect to GitHub repositories.

            1. Task board:
            Open Engineering from the sidebar. Tasks are organized in Kanban columns (Backlog, In Progress, Review, Done). Drag cards or update status from the task modal.

            2. Create a task:
            Click New Task, enter a title, description, priority, and optional assignee. Tasks are scoped to the active product.

            3. Milestones:
            Group tasks into milestones for release planning. Filter the board by milestone to focus on a sprint or launch.

            4. GitHub activity:
            Link repositories in Product Registry. The GitHub tab shows recent commits and pull requests for linked repos.

            5. Assignees:
            Assign tasks to staff members who have access to the product. Assignee lists come from your staff roster.
            """, "engineering", 4),

        new("time-tracking", "Time Tracking & Approvals",
            """
            Accurate time tracking feeds approvals and payroll. Clock times use your device timezone, not the account default.

            1. Clock in and out:
            Go to People → Time & Approvals. Start a live timer with Clock In. Clock Out when you finish your session. Your local timezone is recorded automatically.

            2. Manual time entries:
            Use Log Time to add a past shift with start and end times. Add a note explaining the work if your manager requires it.

            3. Submit for approval:
            Time entries may require manager approval depending on your workflow. Pending items appear in the Approvals tab.

            4. Request edits:
            If a past entry is wrong, submit a correction request with the corrected hours and a reason. Managers approve or reject from Approvals.

            5. Timezone note:
            Each entry stores the IANA timezone from your browser (e.g. Africa/Mogadishu). Traveling staff should verify times look correct before submitting.
            """, "people", 5),

        new("payroll-basics", "Payroll Basics",
            """
            Payroll uses approved time data. Platform admins can run payroll exports; managers review approvals first.

            1. Open Payroll:
            Navigate to People → Payroll from the sidebar. You will see pay periods and run history for the active product.

            2. Approvals first:
            Ensure time entries and edit requests are approved before running payroll. Unapproved time may be excluded from totals.

            3. Run payroll:
            Platform admins can generate a payroll run for a date range. Review totals per person before exporting.

            4. Export CSV:
            Download the payroll CSV for your accounting system. Keep exports secure — they contain sensitive compensation data.

            5. Permissions:
            Only users with platform admin role can execute payroll runs. Contact your administrator if you need access.
            """, "people", 6),

        new("calendar-events", "Calendar & Events",
            """
            The shared calendar helps teams track milestones, renewals, and operational events.

            1. Open Calendar:
            Go to /admin/calendar from the sidebar under Platform.

            2. Add an event:
            Use the Add event form — set title, date, type (meeting, milestone, reminder), and optional description.

            3. Event types:
            Milestones sync conceptually with engineering releases. Reminders help with license renewals and contract dates.

            4. Upcoming view:
            The stats row shows upcoming events and counts for the current week. Scroll the event list for details.

            5. Product scope:
            Calendar events are scoped to the active product tenant.
            """, "platform", 7),

        new("file-management", "File Management",
            """
            Upload and organize operational files — separate from ticket attachments and chat uploads.

            1. Open Files:
            Go to Platform → Files (/admin/platform?tab=files).

            2. Upload a file:
            Set an optional folder path (e.g. /docs/ or /contracts/), then choose a file up to 25 MB. Supported types include PDF, Office documents, and images.

            3. Storage:
            Files are stored in Supabase when the API storage bucket (ops-files) is configured. The public URL column links to the stored file when available.

            4. Folder paths:
            Use consistent folder naming so teammates can find documents. Folder is metadata — organize logically per product.

            5. Troubleshooting:
            If upload fails with "Bucket not found", ask your platform admin to create the ops-files bucket in Supabase.
            """, "platform", 8),

        new("audit-logs", "Audit Logs",
            """
            Audit logs provide a tamper-evident trail of important actions across the platform.

            1. Open Audit:
            Go to Platform → Audit (/admin/platform?tab=audit).

            2. What is logged:
            Actions include ticket changes, assignments, time approvals, asset updates, registry edits, and other sensitive operations.

            3. Search and filter:
            Use the search box to find events by actor, action, or entity type. Results show timestamp, user, and before/after details when available.

            4. Compliance:
            Export or review logs during security reviews. Do not share raw audit exports outside authorized channels.

            5. Retention:
            Retention policy depends on your Supabase and API configuration. Contact your administrator for archival rules.
            """, "platform", 9),

        new("notifications", "Notification Preferences",
            """
            Control which in-app alerts you receive for assignments, approvals, and escalations.

            1. Open Notifications:
            Go to Platform → Notifications (/admin/platform?tab=notifications).

            2. Toggle channels:
            Enable or disable alerts for assignments, approval requests, escalations, and renewal reminders independently.

            3. In-app inbox:
            Unread notifications appear in the list below preferences. Mark items read as you process them.

            4. Email delivery:
            Email notifications depend on API email configuration (e.g. Postmark). In-app prefs always apply.

            5. Per-user settings:
            Preferences are saved per user and per product tenant.
            """, "platform", 10),

        new("product-registry", "Product Registry",
            """
            The Product Registry lists every brand in your KobNeti workspace and links operational metadata.

            1. Open Registry:
            Go to /admin/registry from the sidebar (platform admin sections).

            2. Product cards:
            Each card shows display name, tenant ID, public help center URL, and linked GitHub repositories.

            3. Link repositories:
            Platform admins can add repo URLs for engineering GitHub activity. Use the full GitHub URL format.

            4. Help center URL:
            The public help center link is used for customer-facing KB. Internal docs stay in this Help Center.

            5. Multi-product ops:
            Switch products from the sidebar — registry is the map of everything you manage.
            """, "platform", 11),

        new("asset-management", "Asset Management",
            """
            Track laptops, licenses, and equipment assigned to your team.

            1. Open Assets:
            Go to /admin/assets from the sidebar.

            2. Register an asset:
            Click Register asset, enter name, type (hardware, license, other), renewal date, and optional notes.

            3. Assign and retire:
            Assign available assets to staff from the table actions. Retire equipment when it is decommissioned.

            4. Renewal reminders:
            Use Send renewal reminders to notify about licenses expiring in the next 30 days.

            5. Filters:
            Search by name, serial, or assignee. Filter by type and status to audit inventory.
            """, "platform", 12),

        new("insights-reports", "Insights & Reports",
            """
            Insights aggregates operational metrics and lets you run exportable reports.

            1. Open Insights:
            Go to /admin/insights from the sidebar under System & Analytics.

            2. Overview tab:
            See active chats, open tickets, incidents, engineering tasks in progress, pending approvals, and unread notifications at a glance.

            3. Cross-product tab:
            Platform admins can compare metrics across all products in one view.

            4. Reports tab:
            Run ticket, time, payroll, or audit reports for a date range. Download CSV when the run completes.

            5. When to use:
            Use Insights for standups, weekly ops reviews, and leadership dashboards.
            """, "platform", 13),

        new("internal-chat", "Internal Team Chat",
            """
            Internal Chat is a lightweight team channel for staff coordination — not customer-facing.

            1. Open Internal Chat:
            Go to /admin/internal from the sidebar.

            2. Channels:
            Browse existing channels or create a new channel for a team or project. Channels are scoped to the active product.

            3. Send messages:
            Type in the message box and press Enter. Messages show sender name and timestamp.

            4. Use cases:
            Coordinate incident response, handoffs between shifts, or quick questions that do not need a ticket.

            5. Not a replacement for tickets:
            Customer issues should still be tracked in Support Hub for SLA and history.
            """, "platform", 14),
    ];
}
