# User guide: administration

For Exsensic administrators. Every admin screen is available only to accounts in the **Admin** role; the API refuses admin requests from anyone else, even if they type the address directly.

Screenshots are placeholders, to be captured from staging after the Task 3 deployment.

Sign-in details for the admin account are given to you separately by the system owner. They are never written in this guide, in the repository, or in chat.

## 1. Admin dashboard

1. Sign in at **Sign in** with your admin account.
2. Select **Admin** in the navigation bar. Only admins see this link.
3. The dashboard shows three panels:
   - **Waiting for approval**: new requests that need a decision.
   - **Today's bookings**: everything scheduled for today.
   - **Next 7 days**: confirmed work coming up.
4. Under **Other administration areas**, the buttons **Services**, **Time slots** and **Users** open the management screens in sections 4 to 6.

> **Figure 1.1** Admin dashboard with the three panels and the administration buttons.

## 2. Review, approve or reject a booking

1. On the dashboard, select a booking card, or go to **Admin → Bookings** to search. Use **Status**, **From date** and **To date**, then **Apply filters** (**Clear filters** resets them).
2. Select **Details** on the booking you want.
3. Read the client's answers to the service questions and check the date and time.
4. To approve, continue with section 3 (approving means assigning a staff member).
5. To reject, go to **Unable to accept?**, enter a **Reason for rejection** (required; the client sees it) and select **Reject request**, then confirm.
6. A green message confirms the change. The client is notified in the app.

If the page says someone else changed the booking, another admin or the client acted first. Refresh, review the new state and try again.

> **Figure 2.1** Booking results with filters.
> **Figure 2.2** Booking details with the requirements and the reject form.

## 3. Assign a qualified, free staff member

1. On the booking's **Details** page, open **Assign staff (required)**.
2. The list shows only staff who are qualified for this service **and** free at this time. You can't pick anyone else.
3. Choose a staff member and select **Confirm & assign**, then **Confirm booking** in the dialog.
4. The booking becomes **Confirmed**. The client and the staff member are notified.

If the staff member has been booked elsewhere since the page loaded, you'll see a message asking you to choose another.

> **Figure 3.1** Staff selection with qualified, free staff only.

## 4. Manage services

**Create a service**
1. Go to **Admin → Services** and select **New service**.
2. Enter **Name**, **Category**, **Description** and **Duration in minutes** (15 to 480). **Starting price** is optional.
3. Save. The service appears in the list and in the public catalogue.

A service name must be unique; a duplicate shows an error and nothing is saved.

**Edit a service**
1. On **Services**, select **Edit** next to the service.
2. Change the details and save. Existing bookings keep their original date and time.

**Deactivate (archive) a service**
1. On **Services**, select **Archive** next to the service and confirm.
2. Its state changes to **Archived**. Clients can no longer see or book it. Existing bookings are not affected.
3. To bring it back, select **Restore**.

Services are never deleted, so past bookings always show which service they were for.

> **Figure 4.1** Services list with Edit and Archive actions.
> **Figure 4.2** New service form.

## 5. Manage time slots

**Create slots**
1. Go to **Admin → Time slots** and select **Generate slots**.
2. Enter **From** and **To** dates (at most 90 days apart), tick the **Weekdays**, list the **Start times**, and set the **Duration in minutes**.
3. Select **Generate slots**. The message says how many were created and how many already existed and were skipped, so running it twice never makes duplicates.

**Block a slot**
1. On **Time slots**, choose a date range with **From** and **To**, then **Apply**.
2. Next to a free slot, type a **Reason** (for example "Public holiday") and select **Block**.
3. The slot shows as blocked with its reason, and clients can't book it. Select **Unblock** to reopen it.

A slot that already has an active (requested or confirmed) booking can't be blocked or deleted. The buttons are hidden for it, and the system refuses the change if attempted another way. Reschedule or cancel the booking first.

**Delete a slot**
Select **Delete** next to a slot with no active booking and confirm. Prefer **Block** when you want a record of why the time was unavailable.

> **Figure 5.1** Time slots list with Block, Unblock and Delete.
> **Figure 5.2** Generate slots form.

## 6. Manage users

**View users**
1. Go to **Admin → Users**. Filter by **Role** and select **Apply** (**Clear** resets). Use **Previous** and **Next** to page through.

**Add a staff member**
1. Select **Add staff**.
2. Enter **Full name**, **Email**, **Job title** and a **Password** (at least 10 characters with upper case, lower case and a digit), and tick the **Services they can deliver**.
3. Select **Create staff account**. Give the staff member their password privately and ask them to keep it secret.

Clients create their own accounts by registering. Only the system owner can create admin accounts.

**Enable or disable an account**
1. Select **Deactivate** next to the user and confirm.
2. They are signed out on their next request and can't sign in until reactivated. Their bookings and history are kept.
3. Select **Activate** to restore access.

You can't deactivate your own account. This makes sure the system always has an admin who can sign in.

Changing a user's role isn't available. To give someone a different role, create a new account for them.

> **Figure 6.1** Users list with the role filter and Activate/Deactivate.
> **Figure 6.2** Add staff form.

## 7. Client data permissions

Admins can see more personal information than anyone else, so use it only to run bookings.

| Who | Sees |
|---|---|
| Client | Only their own profile and bookings |
| Staff | Only bookings assigned to them, including that client's name, email and phone |
| Admin | All bookings, and the name, email, role and state of every account |

- Use client details only to deliver the booked service (POPIA purpose limitation).
- Don't copy client details out of the system, into chat or into documents.
- Deactivate accounts that should no longer have access, such as staff who have left.

Full details: [security.md](../security.md) section 6.

## 8. Signing out safely

1. Select **Sign out** in the navigation bar when you finish, especially on a shared computer.
2. Your session also ends automatically after 30 minutes; you'll be asked to sign in again.
3. Close the browser on a shared or public computer.
4. If you think someone else has used your account, tell the system owner straight away so the password can be changed.

> **Figure 8.1** Navigation bar with Sign out.
