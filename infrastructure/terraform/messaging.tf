# The event bus, the Notification queues and their dead-letter queues, and the
# rules that route bus events to those queues.
#
# One queue per consumer, and fan-out belongs to the bus rather than to the
# producers: adding a consumer later means adding a queue and a rule, not
# changing Orders or Shipping.
#
# The bus and all four queues carry `prevent_destroy`. These are the resources
# Luna's services depend on at runtime, and Terraform cannot know whether the
# running code still needs them, so the guard is unconditional. Removing the
# block is a deliberate act, and the correct sequence for a queue that is no
# longer used is: stop the code that uses it, deploy that change, confirm
# nothing is writing or reading, and only then remove the block. Deleting a
# resource in the same change that stops using it breaks the running version.

resource "aws_cloudwatch_event_bus" "luna" {
  name = var.bus_name

  # Deleting the bus would fail every rule on it and silently drop published
  # events, so it is protected like the queues.
  lifecycle {
    prevent_destroy = true
  }
}

locals {
  # detail-type -> queue name. The routing contract lives in one place.
  notification_order_events    = ["OrderConfirmed"]
  notification_shipment_events = ["ShipmentCreated", "ShipmentInTransit", "ShipmentDelivered"]

  notification_queue_common = {
    visibility_timeout_seconds = var.visibility_timeout_seconds
    message_retention_seconds  = var.message_retention_seconds
    receive_wait_time_seconds  = var.receive_wait_time_seconds
  }
}

# ---------------------------------------------------------------- orders queue

resource "aws_sqs_queue" "notification_orders_dlq" {
  name                      = "luna-notification-orders-dlq"
  message_retention_seconds = var.message_retention_seconds

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_sqs_queue" "notification_orders" {
  name                       = "luna-notification-orders"
  visibility_timeout_seconds = local.notification_queue_common.visibility_timeout_seconds
  message_retention_seconds  = local.notification_queue_common.message_retention_seconds
  receive_wait_time_seconds  = local.notification_queue_common.receive_wait_time_seconds

  # Failed messages land in the DLQ after this many receives. Triaging and
  # re-driving them is Phase 4 work; Phase 2 only requires that it happens and
  # is visible.
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notification_orders_dlq.arn
    maxReceiveCount     = var.dlq_max_receive_count
  })

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_cloudwatch_event_rule" "notification_orders" {
  name           = "luna-notification-orders"
  event_bus_name = aws_cloudwatch_event_bus.luna.name

  event_pattern = jsonencode({
    source      = ["luna.orders"]
    detail-type = local.notification_order_events
  })
}

resource "aws_cloudwatch_event_target" "notification_orders" {
  rule           = aws_cloudwatch_event_rule.notification_orders.name
  event_bus_name = aws_cloudwatch_event_bus.luna.name
  arn            = aws_sqs_queue.notification_orders.arn
}

# ------------------------------------------------------------- shipments queue

resource "aws_sqs_queue" "notification_shipments_dlq" {
  name                      = "luna-notification-shipments-dlq"
  message_retention_seconds = var.message_retention_seconds

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_sqs_queue" "notification_shipments" {
  name                       = "luna-notification-shipments"
  visibility_timeout_seconds = local.notification_queue_common.visibility_timeout_seconds
  message_retention_seconds  = local.notification_queue_common.message_retention_seconds
  receive_wait_time_seconds  = local.notification_queue_common.receive_wait_time_seconds

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notification_shipments_dlq.arn
    maxReceiveCount     = var.dlq_max_receive_count
  })

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_cloudwatch_event_rule" "notification_shipments" {
  name           = "luna-notification-shipments"
  event_bus_name = aws_cloudwatch_event_bus.luna.name

  event_pattern = jsonencode({
    source      = ["luna.shipping"]
    detail-type = local.notification_shipment_events
  })
}

resource "aws_cloudwatch_event_target" "notification_shipments" {
  rule           = aws_cloudwatch_event_rule.notification_shipments.name
  event_bus_name = aws_cloudwatch_event_bus.luna.name
  arn            = aws_sqs_queue.notification_shipments.arn
}
