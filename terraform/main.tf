module "lambda_function" {
  source = "terraform-aws-modules/lambda/aws"

  function_name = "createSqsAndS3Folder"
  description   = "Create S3 Folder and SQS as destination of S3 events"
  handler       = "create_sqs.create_janus_queue_handler"
  runtime       = "python3.8"

  source_path = "../src/createSqsAndS3Folder/create_sqs.py"

  tags = {
    Name = "my-lambda1"
  }
}