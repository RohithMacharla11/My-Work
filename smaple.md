Perfect, everything's clear. One shared `pendingComment` across all three flows, audit log gets the comment text in the comment field and file changes in attachmentChanges (Flow B only), View Comments gets only the comment text. Save button gated on non-empty text everywhere. Here's the complete final code.

---

## 1. State Changes

```typescript
// Flow B — Delete with mandatory comment, multi-select capable
selectedForDeleteIndexes: number[] = [];
showDeleteCommentModal: boolean = false;
// pendingComment is now SHARED across Flow A, B, C — no separate pendingDeleteComment
```

Your existing `pendingComment`, `pendingAction`, `commentSaved`, `showAddCommentModal` stay as-is.

---

## 2. Action Label Helpers

```typescript
private getCommentActionTag(action: string): string {
  const effectiveRole = this.resolveEffectiveRole();

  if (action === 'add') return `${effectiveRole}_Comment`;
  if (action === 'delete') return `${effectiveRole}_DocDelete`;

  const map: Record<string, Record<string, string>> = {
    SenderMaker:     { submit: 'SM_Remark', draft: 'SM_Draft' },
    SenderChecker:   { approve: 'SC_Remark', sendback: 'SC_Sendback', reject: 'SC_Reject' },
    ReceiverMaker:   { submit: 'RM_Remark', approve: 'RM_Remark', throwback: 'RM_Throwback' },
    ReceiverChecker: { approve: 'RC_Remark', sendback: 'RC_Sendback' },
  };
  return map[effectiveRole]?.[action] ?? `${effectiveRole}_${action}`;
}

private getAuditAction(action: string): string {
  const map: Record<string, string> = {
    draft:      'Saved and Locked the Record',
    submit:     'Submitted the Record',
    approve:    'Approved the Record',
    throwback:  'Thrown Back the Record',
    sendback:   'Sent Back the Record',
    reject:     'Rejected the Record',
    adminSave:  'Admin Updated the Record',
    add:        'Added a Comment',
    delete:     'Deleted Document(s)',
  };
  return map[action] || action;
}
```

---

## 3. Shared Refresh Helper

```typescript
private async refreshRecordAfterSave(): Promise<void> {
  this.LoadedRecord = this.source === 'archive'
    ? await this.spService.getArchivalListItem(this.editItemId).toPromise()
    : await this.spService.getListItem(this.editItemId).toPromise();

  if (this.showViewCommentsModal) {
    this.parsedComments = this.parseComments(this.LoadedRecord?.Comments || '');
  }
  if (this.showAuditModal) {
    this.parsedAuditLog = this.parseAuditLog(this.LoadedRecord?.AuditLog || '');
  }
}

private async refreshExistingFiles(): Promise<void> {
  const files = this.source?.toLowerCase() === 'archive'
    ? await this.spService.getArchivalFilesInFolder(this.liveid.toString()).toPromise()
    : await this.spService.getFilesInFolder(this.editItemId.toString()).toPromise();

  this.existingFiles = (files as any[]).map((f: any) => ({
    name: f.Name,
    downloadUrl: this.source === 'archive'
      ? this.spService.buildArchivalDownloadUrl(this.liveid.toString(), f.Name)
      : this.spService.buildDownloadUrl(this.editItemId.toString(), f.Name),
    previewUrl: this.source === 'archive'
      ? this.spService.buildArchivalPreviewUrl(this.liveid.toString(), f.Name)
      : this.spService.buildPreviewUrl(this.editItemId.toString(), f.Name),
    uploadedBy: this.source === 'archive'
      ? f.ListItemAllFields.FieldValuesAsText.CreatedByLive?.split('\\').pop()?.trim() || ''
      : f.Author?.LoginName?.split('\\').pop()?.trim() || '',
    uploadedById: this.source === 'archive'
      ? f.ListItemAllFields.CreatedByLiveId || null
      : f.Author?.Id || null,
    uploadedRole: this.getRoleFromUserGroup(),
    markedForDeletion: false,
    isDeleted: f.ListItemAllFields?.isDeleted === true,
    fileItemId: f.ListItemAllFields?.Id || null,
    uploadedOn: this.source === 'archive'
      ? (f.ListItemAllFields.CreatedDateLive ? new Date(f.ListItemAllFields.CreatedDateLive) : null)
      : (f.ListItemAllFields.Modified ? new Date(f.ListItemAllFields.Modified) : null)
  })).sort((a, b) => (b.fileItemId || 0) - (a.fileItemId || 0));
}
```

---

## 4. Master `saveComment()` Dispatcher

```typescript
saveComment(): void {
  if (!this.pendingComment.trim()) return;

  if (this.showDeleteCommentModal) {
    this.saveDeleteComment();
    return;
  }
  if (this.pendingAction) {
    this.saveActionComment();
    return;
  }
  this.saveStandaloneComment();
}
```

---

## 5. Flow A — Standalone Add Comment

```typescript
private async saveStandaloneComment(): Promise<void> {
  this.isLoading = true;
  try {
    const digest = this.source === 'archive'
      ? await this.spService.getArchivalRequestDigest().toPromise()
      : await this.spService.getRequestDigest().toPromise();

    const effectiveRole = this.resolveEffectiveRole();
    const actionTag = this.getCommentActionTag('add');
    const addedBy = this.userService.uid || 'Unknown';
    const addedOn = new Date().toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', hour12: false
    });
    const commentText = this.pendingComment.trim();
    const commentEntry = `${effectiveRole};${actionTag};${addedBy};${addedOn};${commentText}`;
    const existingComments = this.LoadedRecord?.Comments || '';
    const newComments = existingComments ? `${commentEntry}||${existingComments}` : commentEntry;

    const auditEntry = this.buildAuditEntry(
      this.getAuditAction('add'),
      this.LoadedRecord?.Status || 'NA',
      'NA',
      commentText
    );

    const payload = { Comments: newComments, AuditLog: auditEntry };

    if (this.source === 'archive') {
      await this.spService.updateArchivalListItem(digest, this.editItemId, payload).toPromise();
    } else {
      await this.spService.updateListItem(digest, this.editItemId, payload).toPromise();
    }

    await this.refreshRecordAfterSave();
    this.showToast('Comment added successfully', 'success');
    this.cancelComment();
  } catch (err: any) {
    console.error('Add comment error:', err);
    this.showToast('Failed to add comment. Please try again.', 'error');
  } finally {
    this.isLoading = false;
  }
}
```

---

## 6. Flow B — Delete Document(s) with mandatory comment

**Trigger — wherever your existing bulk delete button calls `markSelectedForDeletion()`, change it to open the modal instead:**

```typescript
openDeleteCommentModal(): void {
  if (this.selectedFileIndexes.length === 0) return;
  this.pendingComment = '';
  this.showDeleteCommentModal = true;
  document.body.style.overflow = 'hidden';
}
```

**HTML — update your existing bulk delete button:**

```html
<button class="btn-bulk-delete" 
  [disabled]="selectedFileIndexes.length === 0"
  (click)="openDeleteCommentModal()">
  Delete Selected ({{ selectedFileIndexes.length }})
</button>
```

**The save handler:**

```typescript
private async saveDeleteComment(): Promise<void> {
  if (this.selectedFileIndexes.length === 0) {
    this.cancelDeleteComment();
    return;
  }

  this.isLoading = true;
  try {
    const digest = this.source === 'archive'
      ? await this.spService.getArchivalRequestDigest().toPromise()
      : await this.spService.getRequestDigest().toPromise();

    const filesToDelete = this.selectedFileIndexes
      .map(i => this.existingFiles[i])
      .filter(f => f && !f.isDeleted);

    const fileNames = filesToDelete.map(f => f.name).join(', ');

    for (const file of filesToDelete) {
      const deletedName = `deleted_${file.name}`;
      await this.spService.updateFileItem(
        digest,
        file.fileItemId!,
        { isDeleted: true, FileLeafRef: deletedName }
      ).toPromise();
    }

    const effectiveRole = this.resolveEffectiveRole();
    const actionTag = this.getCommentActionTag('delete');
    const addedBy = this.userService.uid || 'Unknown';
    const addedOn = new Date().toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', hour12: false
    });
    const commentText = this.pendingComment.trim();
    // Comment field gets ONLY the comment text — file names go in attachmentChanges
    const commentEntry = `${effectiveRole};${actionTag};${addedBy};${addedOn};${commentText}`;
    const existingComments = this.LoadedRecord?.Comments || '';
    const newComments = existingComments ? `${commentEntry}||${existingComments}` : commentEntry;

    const attachmentChanges = `Deleted: ${fileNames}`;
    const auditEntry = this.buildAuditEntry(
      this.getAuditAction('delete'),
      this.LoadedRecord?.Status || 'NA',
      attachmentChanges,
      commentText
    );

    const payload = { Comments: newComments, AuditLog: auditEntry };
    if (this.source === 'archive') {
      await this.spService.updateArchivalListItem(digest, this.editItemId, payload).toPromise();
    } else {
      await this.spService.updateListItem(digest, this.editItemId, payload).toPromise();
    }

    await this.refreshExistingFiles();
    await this.refreshRecordAfterSave();

    this.showToast(`${filesToDelete.length} document(s) deleted successfully`, 'success');
    this.selectedFileIndexes = [];
    this.cancelDeleteComment();
  } catch (err: any) {
    console.error('Delete document error:', err);
    this.showToast('Failed to delete document(s). Please try again.', 'error');
  } finally {
    this.isLoading = false;
  }
}

cancelDeleteComment(): void {
  // Selection stays — only revert the "marked for deletion" intent, not the multi-select itself
  this.pendingComment = '';
  this.showDeleteCommentModal = false;
  document.body.style.overflow = '';
}
```

Note: I'm using your existing `selectedFileIndexes` (the same array your current bulk-select checkboxes already populate) — not a new array — since you confirmed it's the same multi-select mechanism as before, just the trigger button now opens a modal instead of marking immediately.

---

## 7. Flow C — Throwback/Sendback

**`onSave()` — remove the old defer-and-reopen block, leave everything else identical:**

```typescript
async onSave(action: 'draft' | 'submit' | 'approve' | 'throwback' | 'sendback' | 'reject' | 'adminSave'): Promise<void> {
  console.log("onSave called");
  console.log(this.permissions);
  if(!this.permissions?.canAdminSave && this.permissions?.isReadOnly) {
    this.showToast("You do not have permissions to edit this form",'error');
    return;
  }

  // Old gating block REMOVED — Flow C now always opens modal first via button click, never reaches onSave directly until comment is saved

  if(action === 'submit' && !this.validateBeforeSubmit()) return;
  if(action === 'draft') this.draftSaved = true;
  // ...rest unchanged exactly as before
```

**The trigger handler:**

```typescript
private saveActionComment(): void {
  this.commentSaved = true;
  this.showAddCommentModal = false;
  document.body.style.overflow = '';

  const action = this.pendingAction;
  this.pendingAction = '';
  const savedComment = this.pendingComment;
  this.pendingComment = '';
  
  this.onSave(action as any);
}
```

**HTML — Throwback/Sendback buttons always route through the modal:**

```html
<button *ngIf="p?.canThrowback" class="btn-warning" 
  (click)="pendingAction='throwback'; openAddCommentModal(true)">
  Throwback
</button>

<button *ngIf="p?.canSendBack" class="btn-warning" 
  (click)="pendingAction='sendback'; openAddCommentModal(true)">
  Sendback
</button>
```

---

## 8. `cancelComment()` — shared cancel for Flow A & C

```typescript
cancelComment(): void {
  this.pendingComment = '';
  this.commentSaved = false;
  this.pendingAction = '';
  this.showAddCommentModal = false;
  this.addCommentWarning = false;
  document.body.style.overflow = '';
}
```

---

## 9. HTML — Save button gating (Add Comment modal AND Delete modal)

```html
<!-- Add Comment Modal (Flow A & C share this) -->
<button class="btn-bnpp-green" [disabled]="!pendingComment.trim()" (click)="saveComment()">
  Save Comment
</button>
```

```html
<!-- Delete Comment Modal (new, Flow B) -->
<div class="comment-modal-backdrop" *ngIf="showDeleteCommentModal" (click)="cancelDeleteComment()">
  <div class="comment-modal-box" (click)="$event.stopPropagation()">
    <div class="comment-modal-header">
      <h3>Confirm Document Deletion</h3>
      <span class="close-x" (click)="cancelDeleteComment()">×</span>
    </div>
    <div class="comment-modal-body">
      <p class="delete-file-summary">
        Deleting {{ selectedFileIndexes.length }} document(s):
        <strong>{{ selectedFileIndexes.map(i => existingFiles[i]?.name).join(', ') }}</strong>
      </p>
      <p class="comment-mandatory-note">⚠ A comment is mandatory to delete document(s).</p>
      <textarea [(ngModel)]="pendingComment" placeholder="Enter reason for deletion..." rows="5"></textarea>
    </div>
    <div class="comment-modal-footer">
      <button class="btn-secondary" (click)="cancelDeleteComment()">Cancel</button>
      <button class="btn-bnpp-green" [disabled]="!pendingComment.trim()" (click)="saveComment()">
        Save & Delete
      </button>
    </div>
  </div>
</div>
```

---

That's the complete set. Drop these in and you're done — no more pieces missing.