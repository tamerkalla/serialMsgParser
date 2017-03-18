// Required libraries and namespaces //
#include <iostream>
#include <string>

using namespace std;

//////// Constants definitions ///////
//CHANGE bytes
//Serial message constants
#define START_BYTE 0xFF
#define SEPARATOR_BYTE 0xFE
#define MIN_START_BYTES 5
#define MAX_MESSAGE_SIZE 100
#define PAYLOAD_UNIT_SIZE 5
#define CHECKSUM_SIZE 4
#define BYTES_TO_READ 1

//Functions return constants
#define TRUE 1
#define FALSE 0
#define REPLY_OK 0
#define REPLY_BAD_CHECKSUM 1
#define REPLY_BAD_SEPARATORS 2

///////////////////////////// Functions Declarations ///////////////////////////////////////

//Function that computes the checksum for given data message
void compute_checksum(unsigned char* checksum, unsigned char* data, int len);

//Function that checks if the message has the correct checksum and proper data separators
int check_packet(unsigned char* data, int len);

void parse_serial();

//Function that finds the start of the message on serial in an infinite loop
int find_message();

//Function that reads the message after the message is found
int read_message(unsigned char* message);

int read(unsigned char* buf, unsigned int count);
int get_message_size_from_message_id(int message_id);
void process_message(unsigned char* buffer, unsigned int message_id);

///////////////////////////////// Start of program //////////////////////////////////////

/*
	Function: main
	The start of the program
   
	Returns:
		Error - Returns 0 when there are no errors in running the program

*/
int main()
{
	//Start parsing serial data
	parse_serial();
	return 0;
}

///////////////////////////// Functions Implementations ///////////////////////////////////////

/*
	Function: parse_serial
		Parses the serial line for messages in an infinite loop
*/
void parse_serial(){
	//Initialize message variables
    unsigned char message[MAX_MESSAGE_SIZE];
	int message_found = FALSE;
	int message_is_valid = FALSE;
    //CHANGE
    //Keep Processing Serial Messages
    for (int j = 0; j < 3; j++){  //while(True){
		
		//Find the beginning of the message after the start flag
		message_found = find_message();
		
		//Read message when the message is found
		if(message_found){
			message_is_valid = read_message(message);
		}
		
		//process valid messages
		if (message_found and message_is_valid){
			//Get message ID from message
			unsigned int message_id = (unsigned int) message[0];
			//Process valid messages
			process_message(message, message_id);
		}
		
		//Reset message variables to parse the next message
		message_found = FALSE;
		message_is_valid = FALSE;        
  }
}

/*
	Function: find_message
		Finds the start of the message on the serial in an infinite loop check
	Returns:
		message_found - returns TRUE when a message is found
*/
int find_message(){
	//Initialize the number of start bytes detected to none
	int start_bytes = 0;
	//Allocate memory for the byte to be read
	unsigned char byte_buff[BYTES_TO_READ];
	//Assume message is not found
	int message_found = FALSE;
	
	//Loop until start flag is detected 
	//A minimum of MIN_START_BYTES must be detected in a row
	while(start_bytes < MIN_START_BYTES){
		
		// Attempt to read one byte
		int number_bytes_read = read(byte_buff, BYTES_TO_READ);
		
		//Check byte value if we were able to read a byte
		if(number_bytes_read > 0){
			
			//Save the read byte
			unsigned char byte_read = byte_buff[0];
			
			//Check if the byte read is a start byte
			if (byte_read == START_BYTE){
				//Increment number of start bytes when a start byte is detected
				start_bytes++;
			}else{
				//Reset the start bytes counter when reading a none-start byte
				start_bytes = 0;
			}
			cout << "start_bytes: " << start_bytes << "\n";
		}else{
			//CHANGE
			break;
		}
	}
	
	if (start_bytes >= MIN_START_BYTES){
		message_found = TRUE;
	}else{
		message_found = FALSE;
	}
	
	//Return True when the message is found
	return message_found;
}

/*
	Function: read_message
		Reads the message after the message is found
   
	Parameters:
		message - the message data to read and validate
	
	Returns:
		message_is_valid - returns TRUE if message is valid
*/
int read_message(unsigned char* message){
	//Initialize message variables
	int message_read = FALSE;
	//Assume message is valid
	int message_is_valid = TRUE;
	int message_index = 0;
	int message_size = 0;
	//Allocate memory for byte to read
	unsigned char byte_buff[BYTES_TO_READ];
	
	//Read Message from serial
	while(!message_read and message_is_valid){
		
		// Attempt to read one byte
		int number_bytes_read = read(byte_buff, BYTES_TO_READ);
		
		//Check byte value if we were able to read a byte
		if(number_bytes_read > 0){
			
			//Save the read byte
			unsigned char byte_read = byte_buff[0];
			cout << "BYTE: " << (unsigned int) byte_buff[0] << "\n";

			//Get message size from the first byte received
			if(message_index == 0){
				//Calculate message size
				unsigned int message_id = (unsigned int)byte_read;
				message_size = get_message_size_from_message_id(message_id);
				
				//Check if the message size is valid
				//Total message size with start bytes should not exceed MAX_MESSAGE_SIZE
				if (message_size +  MIN_START_BYTES > MAX_MESSAGE_SIZE){
					cout << "Invalid Message Size\n";
					message_is_valid = FALSE;
					break;
				}
			}
			
			//Read the rest of the message if the message is valid
			message[message_index] = byte_read;
			//Increment array index
			message_index++;
			
			//Check if we read the entire message
			if (message_index >= message_size){
				message_read = TRUE;
			}
		}
	}
	
	//Validate checksum and separators if message was read
	if(message_read and message_is_valid){
		//Check packet for correct data
		if (check_packet(message, message_size) != REPLY_OK){
			message_is_valid = FALSE;
		}
	}
	
	//Return the validity of the message
	return message_is_valid;
}

/*
	Function: compute_checksum
	Computes the checksum for given data message.
	Note: The calculate checksum is returned in checksum parameter.
	The checksum array must have memory allocated with a minimum size of CHECKSUM_SIZE 
   
	Parameters:
		checksum - checksum array used to return the result 
		data - the message data used to compute the checksum
		len - length of data array
*/
void compute_checksum(unsigned char* checksum, unsigned char* data, int len){
	
	//Initialize the checksum array to zeros before calculating checksum
	for (int i = 0; i < CHECKSUM_SIZE; i++){
		checksum[i] = 0x00;
	}
	
	//Loop through the data and XOR based on the payload index
	for(int j = 0 ;j < len; j++){
		//Calculate the current payload index
		int payload_index = j % PAYLOAD_UNIT_SIZE;
		
		//Calculate the checksum on the data only 
		//and ignore the separator bytes at the end of the payload
		if(payload_index < PAYLOAD_UNIT_SIZE - 1){
			//XOR data with previous data at the same payload index to calculate checksum
			checksum[payload_index] ^= data [j];
		}
	}
}

/*
	Function: check_packet
		Checks if the message has the correct checksum and proper data separators
   
	Parameters:
		data - the message data to validate
		len - length of data array
	
	Returns:
		status - returns REPLY_OK if message is valid, 
						 REPLY_BAD_CHECKSUM for bad checksum, 
						 REPLY_BAD_SEPARATORS for missing separators 
*/
int check_packet(unsigned char* data, int len){
	//Assume the packet is okay before checking for errors
	int status = REPLY_OK;
	
	//Calculate checksum of packet
	//Calculated checksum array memory allocation
	unsigned char checksum[CHECKSUM_SIZE];
	
	//calculate the actual checksum index in the data array. 
	// The index is at the beginning of the last payload
	int actual_checksum_index = len - PAYLOAD_UNIT_SIZE;
	
	//Pass the data payloads only and stop before the actual checksum payload at the end
	compute_checksum(checksum, data, actual_checksum_index);
	
	//Validate that the checksum is correct by looping over the calculated checksum
	for(int i = 0; i < CHECKSUM_SIZE; i++){
		//Invalidate the message if any of the checksum bytes is incorrect
		if(checksum[i] != data[actual_checksum_index + i]){
			status = REPLY_BAD_CHECKSUM;
			break;
		}
	}
	
	//Check that the separators bytes are in the message
	//Calculate the number of payloads
	int number_of_paylods = len/PAYLOAD_UNIT_SIZE;
	
	//The separator byte is located at the end of the payload
	int sperator_byte_index = PAYLOAD_UNIT_SIZE - 1;
	
	//Check that all the payloads end with separators
	for(int j = 0; j < number_of_paylods; j++){
		int payload_start_index = j * PAYLOAD_UNIT_SIZE;
		//Invalidate Message if any of the separators are missing at the end of the payloads
		if(data[payload_start_index + sperator_byte_index] != SEPARATOR_BYTE){
			status = REPLY_BAD_SEPARATORS;
			break;
		}
	}
	
	//return the message validity
	return status;
}
//#Change
unsigned int lastcount = 0;
int read(unsigned char* buf, unsigned int count){
    unsigned int size = 25;
    unsigned char bufread[size] = {0x00,START_BYTE,0x00,START_BYTE,START_BYTE,START_BYTE,START_BYTE,START_BYTE,0x0F,0x01,0x02,0x03,SEPARATOR_BYTE,0x01,0x02,0x03,0x04,SEPARATOR_BYTE,0x0E,0x03,0x01,0x07,SEPARATOR_BYTE,0x00};
    
    if (lastcount<size){
        for (unsigned int i=0;i<count;i++){
            buf[i] = bufread[i+lastcount];
            cout << "READ " << i + lastcount<<"\n";
        }
		lastcount+=count;
		return count;
    }else{
		return 0;
	}
    
    
}

int get_message_size_from_message_id(int message_id){
    return message_id;
}

void process_message(unsigned char* buffer, unsigned int message_id){
    cout << "message processed\n";
	cout << (unsigned int) buffer[0] << " " << message_id << "\n";
}

