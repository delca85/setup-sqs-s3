# data "archive_file" "lambda_source_package" {
#   type        = "zip"
#   output_path = "${path.module}/tmp/lambda_sqs_s3.zip"

#   source_file = "${path.module}/lambda_sqs_s3_src/create_sqs.py"

#   excludes = [
#     "__pycache__",
#     "core/__pycache__",
#     "tests"
#   ]
# }


module "lambda_function" {
  source = "terraform-aws-modules/lambda/aws"

  function_name      = "createSqsAndS3Folder"
  description        = "Create S3 Folder and SQS as destination of S3 events"
  handler            = "create_sqs.create_janus_queue_handler"
  runtime            = "python3.8"
  publish            = true
  attach_policy_json = true
  attach_policy_json = true
  policy_json        = <<EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "sqs:CreateQueue",
                "sqs:ListQueues"
            ],
            "Resource": ["*"]
        }
    ]
}
EOF

  source_path = "${path.module}/lambda_sqs_s3_src/create_sqs.py"

  layers = [
    module.lambda_layer_local.lambda_layer_arn
  ]

  tags = {
    Name = "marvel-lambda"
  }
}

module "lambda_layer_local" {
  source       = "terraform-aws-modules/lambda/aws"
  create_layer = true

  layer_name          = "sqs-s3-lambda-local-layer"
  description         = "Create S3 Folder and SQS as destination of S3 events function layer (deployed locally)"
  compatible_runtimes = ["python3.8"]

  source_path = "${path.module}/lambda_sqs_s3_src"
}