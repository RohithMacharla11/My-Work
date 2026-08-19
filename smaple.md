/* MDC compatibility layer — @once targets pre-MDC selectors */

.mat-mdc-form-field {
  width: 100%;
}

.mat-mdc-form-field-infix {
  min-height: 48px;
}

/* underline — @once used #b4babf */
.mat-mdc-form-field .mdc-line-ripple::before {
  border-bottom-color: #b4babf;
}

.mat-mdc-form-field.mat-form-field-disabled .mdc-line-ripple::before {
  border-bottom-color: #f0f1f2;
}

/* label colour — @once used #5c6166 */
.mat-mdc-form-field .mdc-floating-label,
.mat-mdc-input-element::placeholder {
  color: #5c6166;
}

/* hint — @once used #17181a */
.mat-mdc-form-field-hint {
  color: #17181a;
  font-size: 12px;
}

/* dialog */
.mat-mdc-dialog-surface {
  padding: 24px;
}

.mat-mdc-dialog-content {
  max-height: 70vh;
}