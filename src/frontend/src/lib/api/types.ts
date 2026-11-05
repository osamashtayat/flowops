import type { components } from './schema'

/**
 * Names for the generated schema types the app uses. These are aliases only — every shape comes
 * from `schema.d.ts` (generated from contracts/openapi.json by `npm run api:types`), so a contract
 * change shows up as a compile error here, never as drift.
 */
export type Schemas = components['schemas']

export type CurrentUserDto = Schemas['CurrentUserDto']
export type Role = Schemas['Role']
export type Capability = Schemas['Capability']
export type MoneyDto = Schemas['MoneyDto']
export type UserRef = Schemas['UserRef']

export type RequestStatus = Schemas['RequestStatus']
export type RequestStage = Schemas['RequestStage']
export type RequestType = Schemas['RequestType']
export type LeaveType = Schemas['LeaveType']
export type SubmitPurchaseRequest = Schemas['SubmitPurchaseRequest']
export type SubmitLeaveRequest = Schemas['SubmitLeaveRequest']
export type RequestDetailsDto = Schemas['RequestDetailsDto']
export type ActivityDto = Schemas['ActivityDto']
export type AddCommentRequest = Schemas['AddCommentRequest']
export type ActivityType = Schemas['ActivityType']
export type TaskAction = Schemas['TaskAction']
export type WorkTaskDto = Schemas['WorkTaskDto']
export type AvailableActionsDto = Schemas['AvailableActionsDto']
export type RequestScope = Schemas['RequestScope']
export type PagedRequestSummaries = Schemas['PagedResultOfRequestSummaryDto']
export type RequestSummaryDto = Schemas['RequestSummaryDto']
export type WorkTaskOutcome = Schemas['WorkTaskOutcome']
export type InboxItemDto = Schemas['InboxItemDto']
export type PagedInboxItems = Schemas['PagedResultOfInboxItemDto']
export type UnreadCountDto = Schemas['UnreadCountDto']

export type ProblemDetails = Schemas['ProblemDetails']
export type ValidationProblemDetails = Schemas['ValidationProblemDetails']
export type LoginRequest = Schemas['LoginRequest']
export type ChangePasswordRequest = Schemas['ChangePasswordRequest']
