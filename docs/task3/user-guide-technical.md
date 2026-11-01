# User guide: getting access, technical requirements and help

For everyone who uses Exsensic: clients, staff and administrators. This part of the guide covers getting access, what you need on your computer, how accounts are created, the settings the system uses, backup, where to get help, common problems and a glossary. The step-by-step task pages for each role are in the client, staff ([user-guide-staff.md](user-guide-staff.md)) and admin ([user-guide-admin.md](user-guide-admin.md)) sections.

Screenshots are placeholders, to be captured from the live system after the final release. Sign-in details are given privately by an administrator. They are never written in this guide, in the repository, or in chat.

## 1. What Exsensic is

Exsensic is a booking and service management system for a creative agency. Clients book photoshoots, website consultations and social-media consultations online. Staff see the work assigned to them. Administrators approve bookings, assign staff and manage services, time slots and users.

A booking always has one of four statuses:

| Status | Meaning |
|---|---|
| Requested | The client has submitted the booking and it is waiting for an administrator |
| Confirmed | An administrator approved it and assigned a staff member |
| Completed | The staff member has marked the work as done |
| Cancelled | The client cancelled it, or an administrator rejected it (a rejection is a cancellation with a reason) |

## 2. Getting access

| Site | Address | Use it for |
|---|---|---|
| Production (live) | https://app-exsensic-web.azurewebsites.net | The real, current system |
| Staging (test) | https://app-exsensic-web-staging.azurewebsites.net | Trying things out and rehearsals; a copy with the same features |

Open the address in a web browser; nothing needs to be installed. The first request after a quiet period can take up to about a minute while the system wakes up (see section 9). If the page does not appear straight away, wait and reload once.

### What each role can do

| | Client | Staff | Administrator |
|---|---|---|---|
| Browse services and see available time slots | Yes | | |
| Book, reschedule and cancel your own bookings | Yes | | |
| See your own bookings and their history | Yes | | |
| See bookings assigned to you and the client's requirements | | Yes | |
| Mark an assigned booking completed | | Yes | |
| See all bookings, approve or reject them, assign staff | | | Yes |
| Manage services, time slots and user accounts | | | Yes |

You can only do what your role allows. If you try to open something that is not yours, the system answers "not found" instead of showing it.

## 3. How accounts are created

| Role | How you get an account |
|---|---|
| Client | Select **Register** on the home page, fill in the form and sign in. Passwords need at least 10 characters, with an upper-case letter, a lower-case letter and a digit |
| Staff | An administrator creates your account and gives you your sign-in details privately. Staff cannot register themselves |
| Administrator | The first administrator account is created when the system is first set up. Further accounts are managed by an existing administrator |

If you forget your password, ask an administrator. There is no self-service reset.

> **Figure 3.1** The Register page and the Sign in page.

## 4. Supported browsers and devices

You need a current version of a modern browser with JavaScript and cookies switched on, and an HTTPS connection (the sites only work over HTTPS).

| Browser | Status |
|---|---|
| Google Chrome (current) | Tested: automated browser tests and accessibility checks run on Chromium |
| Microsoft Edge (current) | Expected to work (same engine as Chrome); not separately tested |
| Mozilla Firefox (current) | Expected to work; not separately tested |
| Safari (current) | Expected to work; not separately tested |

Update this table with your own results if you test another browser before submitting.

Screen sizes: the layout adapts from a 360-pixel-wide phone to a desktop. You can use a phone, a tablet or a computer.

Other things you need:

- A normal internet connection. There is nothing to download.
- Cookies allowed for the Exsensic address. Signing in uses a cookie, and your session ends after 30 minutes without use, after which you sign in again.
- Pop-up blockers do not matter; Exsensic does not use pop-ups.

## 5. Signing in and out

1. Open the site and select **Sign in**.
2. Enter your email address and password, then select **Sign in**.
3. When you finish, select **Sign out**. On a shared computer, always sign out.

If sign-in fails, the message is the same whether the email or the password was wrong. After five wrong tries in a row, sign-in is paused for 15 minutes, even if you then type the right password. Wait, then try again.

## 6. Optional: running Exsensic on your own computer

Most people never need this. It is for the project team and for markers who want to run the code.

You need the .NET 10 SDK and SQL Server LocalDB (or any SQL Server). The steps, in short:

1. Get the code from https://github.com/Josh0123456/exsensic-insy7315 and run `dotnet restore`.
2. Give the API its settings with `dotnet user-secrets` (section 7 lists the names; you choose your own values).
3. Start the API, then the website, with the `https` launch profile.
4. Open https://localhost:7005.

The full commands are in the README under **Getting started**. Run `dotnet test` to run the unit and integration tests.

## 7. Settings the system uses (names only)

The system reads these settings. **The values are never in this guide or in the code repository**; they are kept in the Azure App Service settings for the live system, and in `dotnet user-secrets` on a developer's own computer.

| Setting | Used by | What it is for |
|---|---|---|
| `ConnectionStrings__Default` | API | How the API reaches its database |
| `Jwt__SigningKey` | API | Signs the sign-in tokens (different in each environment) |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` | API | Token settings (not secret) |
| `Seed__AdminEmail`, `Seed__AdminPassword`, `Seed__DemoPassword` | API | The first administrator and the demo accounts |
| `BookingPolicy__ClientCancelCutoffHours` | API | How close to the start time a client can still cancel (24 hours) |
| `Database__MigrateOnStartup`, `Database__SeedDemoData` | API | Applies database updates and demo data at start-up |
| `Api__BaseUrl` | Website | The address the website uses to call the API |
| `ASPNETCORE_ENVIRONMENT` | Both | `Staging` or `Production` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Both | Sends monitoring data to Application Insights |

## 8. Backup and recovery

- **Database backups.** The Azure SQL databases keep automatic point-in-time backups for 7 days. An administrator can restore a database to a moment before a problem in the Azure portal (the database, then **Restore**), which creates a copy.
- **Code and releases.** Every change goes through a reviewed pull request, and every release is a numbered version in GitHub. A bad release can be undone by re-running the last good deployment.
- **What is not backed up by you.** Users do not need to back anything up. Bookings are stored in the database.

## 9. Known limits you may notice

- **Slow first page.** The database sleeps when it has been idle, and the first request can take up to about a minute. After that, the system is fast. If you are preparing a demonstration, open the site a minute or two beforehand.
- **A short pause during a release.** When a new version is deployed, the site restarts for a few seconds.
- **The API cannot be opened directly.** The website talks to the API for you; the API only accepts calls from the website.

## 10. Getting help

| If | Do this |
|---|---|
| You cannot sign in | Wait 15 minutes if you typed the wrong password five times, then try again. Otherwise ask an administrator |
| You forgot your password | Ask an administrator |
| Something looks wrong with a booking | Contact an administrator and give the booking reference |
| The site does not load | Wait a minute and reload; then check https://app-exsensic-web.azurewebsites.net/health, which should say Healthy |
| You found a fault | Tell the team (below) with what you did, the time, the page, and a screenshot |

Support contact: the Exsensic project team, through the course's usual channel. Add the agreed email address or channel here before submitting.

Never send your password to anyone, and never include it in a message or screenshot. An administrator will never ask for it.

## 11. Frequently asked questions

**Why does my booking say Requested and not Pending?**
Requested is the first status. It means your request is waiting for an administrator to approve it. There is no separate Pending status.

**Why is there no slot I can pick?**
Slots are created by an administrator. If none show for a service, they are all taken or blocked. Choose another date or service, or contact an administrator.

**The slot I chose has just been taken.**
Two people tried to book the same slot at once and the other one was first. Go back to step 1, refresh the slots and choose another. Do not press Submit repeatedly.

**Can I cancel?**
Yes, up to 24 hours before the start time. After that the system refuses, because the team has already planned the work. Contact an administrator if you need a change.

**What happens if I reschedule a confirmed booking?**
It goes back to Requested, because the new time needs approval again.

**What does "not found" mean when I open a page?**
The page does not exist or does not belong to you. You can only see your own bookings, or, for staff, the bookings assigned to you.

**It says my session has ended.**
You were signed in for more than 30 minutes without activity. Sign in again.

**The page says something went wrong.**
Reload once. If it keeps happening, tell the team what you were doing and the time.

**Is my information safe?**
The sites only work over HTTPS, passwords are stored as hashes, you can only see your own data, and the team follows the privacy rules in the security document ([security.md](../security.md)).

## 12. Glossary

| Term | Meaning |
|---|---|
| Booking | A request for a service at a chosen date and time |
| Booking reference | The unique code shown when you submit a booking |
| Slot | A date and time when a service can be booked |
| Service | Something Exsensic offers, such as a photoshoot |
| Requested, Confirmed, Completed, Cancelled | The four booking statuses (section 1) |
| Administrator | A team member who approves bookings and manages the system |
| Staff | A team member who delivers the booked work |
| Production | The live system real people use |
| Staging | A test copy of the live system |
| HTTPS | The secure way a browser talks to a website (the padlock) |
| Cookie | A small file the browser keeps so you stay signed in |
| API | The part of Exsensic that holds the rules and the data; the website talks to it |
| Health page | A page (`/health`) that answers "Healthy" when the site is running |

## 13. Document control

Version: 1.0. Owner: Josh (technical sections). Last reviewed: update on the day of submission. Related documents: [operations.md](../operations.md) (how the system is hosted and released), [chapter 7](chapter-07-hosting-cicd-operations.md).
