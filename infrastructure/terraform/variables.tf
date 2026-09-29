variable "region" {
  description = "Region used locally and on the server. Phase 15 may set this per environment."
  type        = string
  default     = "us-east-1"
}

variable "endpoint" {
  description = <<-EOT
    Emulator endpoint.

      Local:  http://localhost:4566
      Server: http://floci:4566  (the emulator is reached by container name
                               inside the Compose network, not by localhost)
      AWS:    leave empty to use the real AWS endpoints
  EOT
  type        = string
  default     = "http://localhost:4566"
}

variable "bus_name" {
  description = "Custom event bus that Luna services publish domain events to."
  type        = string
  default     = "luna-bus"
}

variable "sender_address" {
  description = "Verified SES identity used as the From address for customer email."
  type        = string
  default     = "luna@example.test"
}

variable "dlq_max_receive_count" {
  description = <<-EOT
    Receives before a message is moved to the dead-letter queue.

    Kept low so failures surface quickly while the system is being learned.
    Phase 4 owns backoff classification and DLQ triage.
  EOT
  type        = number
  default     = 3
}

variable "visibility_timeout_seconds" {
  description = <<-EOT
    How long a received message stays hidden from other consumers.

    Must exceed the longest expected consumer processing time, or a second
    consumer picks up work that is still running.
  EOT
  type        = number
  default     = 30
}

variable "message_retention_seconds" {
  description = "How long a message survives in its queue before expiring."
  type        = number
  default     = 345600 # 4 days, the AWS default.
}

variable "receive_wait_time_seconds" {
  description = <<-EOT
    Long-polling wait for the consumer loop. 20 is the maximum AWS allows and
    keeps empty polls from becoming a request flood.
  EOT
  type        = number
  default     = 20
}
